using Children_s_toy_shop_management_software.Data;
using Children_s_toy_shop_management_software.Models.Products;
using Children_s_toy_shop_management_software.Models.Staff;
using Children_s_toy_shop_management_software.Models.Customers;
using Children_s_toy_shop_management_software.Models.Reports;
using Children_s_toy_shop_management_software.Models.Inventory;
using Children_s_toy_shop_management_software.Models.POS;
using Children_s_toy_shop_management_software.Models.Dashboard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace Children_s_toy_shop_management_software.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class PortalController : Controller
    {
        private readonly ProductsRepository _productsRepo;
        private readonly StaffRepository _staffRepo;
        private readonly CustomersRepository _customersRepo;
        private readonly ReportsRepository _reportsRepo;
        private readonly InventoryRepository _inventoryRepo;
        private readonly PosRepository _posRepo;
        private readonly DashboardRepository _dashboardRepo;
        private readonly IWebHostEnvironment _env;

        public PortalController(
            ProductsRepository productsRepo,
            StaffRepository staffRepo,
            CustomersRepository customersRepo,
            ReportsRepository reportsRepo,
            InventoryRepository inventoryRepo,
            PosRepository posRepo,
            DashboardRepository dashboardRepo,
            IWebHostEnvironment env)
        {
            _productsRepo = productsRepo;
            _staffRepo = staffRepo;
            _customersRepo = customersRepo;
            _reportsRepo = reportsRepo;
            _inventoryRepo = inventoryRepo;
            _posRepo = posRepo;
            _dashboardRepo = dashboardRepo;
            _env = env;
        }

        public async Task<IActionResult> Dashboard(string? detailBy, string? metric)
        {
            ViewData["Title"] = "Dashboard";
            var normalizedDetail = NormalizeDashboardDetail(detailBy);
            var normalizedMetric = NormalizeDashboardMetric(metric);

            var vm = await _dashboardRepo.GetKpisAsync(normalizedDetail);
            vm.ActiveDetailBy = normalizedDetail;
            vm.ActiveMetric = normalizedMetric;
            return View("Dashboard", vm);
        }

        [HttpGet]
        public async Task<IActionResult> DashboardData(string? detailBy)
        {
            var vm = await _dashboardRepo.GetKpisAsync(detailBy);
            return Json(vm);
        }

        private static string NormalizeDashboardDetail(string? detailBy)
        {
            var d = string.IsNullOrWhiteSpace(detailBy) ? "week" : detailBy.Trim().ToLowerInvariant();
            return d is "week" or "month" or "quarter" or "year" ? d : "week";
        }

        private static string NormalizeDashboardMetric(string? metric)
        {
            var m = string.IsNullOrWhiteSpace(metric) ? "sales" : metric.Trim().ToLowerInvariant();
            return m switch
            {
                "sales" => "sales",
                "orders" => "orders",
                "profit" => "profit",
                "productssold" => "productsSold",
                "productsold" => "productsSold",
                "products_sold" => "productsSold",
                "products-sold" => "productsSold",
                _ => "sales"
            };
        }

        [HttpGet]
        public async Task<IActionResult> Pos(string? search, string? tabId)
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Dashboard");
            }
            return await PosViewAsync(search, tabId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PosScanOrSearch([FromForm] string value, [FromForm] string? currentSearch, [FromForm] string? tabId)
        {
            var currentTabId = NormalizePosTabId(tabId);
            var v = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(v))
            {
                if (IsSpaFragmentRequest())
                {
                    return await PosViewAsync(currentSearch, currentTabId);
                }

                return RedirectToAction(nameof(Pos), new { search = (string?)null, tabId = currentTabId });
            }

            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var cart = GetCartFromSession(currentTabId);

            var product = await _posRepo.FindProductByScanCodeAsync(v);
            if (product != null)
            {
                var addQty = 1;
                var line = cart.Lines.FirstOrDefault(x => string.Equals(x.Code, product.Code, StringComparison.OrdinalIgnoreCase));
                if (line == null)
                {
                    cart.Lines.Add(new PosCartLineVm
                    {
                        Code = product.Code,
                        Name = product.Name,
                        Price = product.Price,
                        Qty = Math.Max(1, Math.Min(addQty, product.StockQuantity))
                    });
                }
                else
                {
                    var nextQty = line.Qty + addQty;
                    line.Qty = Math.Max(1, Math.Min(nextQty, product.StockQuantity));
                }

                SetCartToSession(currentTabId, cart);
                if (IsSpaFragmentRequest())
                {
                    return await PosViewAsync(currentSearch, currentTabId);
                }

                return RedirectToAction(nameof(Pos), new { search = currentSearch, tabId = currentTabId });
            }

           
            SetCartToSession(currentTabId, cart); 
            if (IsSpaFragmentRequest())
            {
                ViewData["Title"] = "POS (Point of Sale)";
                var vm = await BuildPosVmAsync(currentSearch, currentTabId);
                vm.Search = v;
                return View("Pos", vm);
            }

            return RedirectToAction(nameof(Pos), new { search = v, tabId = currentTabId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PosAdd([FromForm] string barcode, [FromForm] int qty, [FromQuery] string? search, [FromQuery] string? tabId)
        {
            try
            {
                var currentTabId = NormalizePosTabId(tabId);
                var cart = GetCartFromSession(currentTabId);
                var product = await _posRepo.FindProductByScanCodeAsync(barcode);
                if (product == null)
                {
                    if (IsSpaFragmentRequest())
                    {
                        ViewData["Title"] = "POS (Point of Sale)";
                        var vm = await BuildPosVmAsync(search, currentTabId);
                        vm.Error = "No product matched this code. Check the barcode or search by name.";
                        return View("Pos", vm);
                    }

                    TempData["PosError"] = "No product matched this code. Check the barcode or search by name.";
                    return RedirectToAction(nameof(Pos), new { search, tabId = currentTabId });
                }

                var addQty = qty <= 0 ? 1 : qty;
                var line = cart.Lines.FirstOrDefault(x => string.Equals(x.Code, product.Code, StringComparison.OrdinalIgnoreCase));
                if (line == null)
                {
                    cart.Lines.Add(new PosCartLineVm
                    {
                        Code = product.Code,
                        Name = product.Name,
                        Price = product.Price,
                        Qty = Math.Max(1, Math.Min(addQty, product.StockQuantity))
                    });
                }
                else
                {
                    var nextQty = line.Qty + addQty;
                    line.Qty = Math.Max(1, Math.Min(nextQty, product.StockQuantity));
                }

                SetCartToSession(currentTabId, cart);
                if (IsSpaFragmentRequest())
                {
                    return await PosViewAsync(search, currentTabId);
                }

                return RedirectToAction(nameof(Pos), new { search, tabId = currentTabId });
            }
            catch (Exception ex)
            {
                return Content("CUSTOM_ERROR_POSADD_" + ex.Message + " | " + ex.StackTrace, "text/plain");
            }
        }

        [HttpGet]
        public async Task<IActionResult> PosMemberLookup([FromQuery] string phone)
        {
            var member = await _posRepo.FindMemberCustomerByPhoneAsync(phone);
            if (member == null)
            {
                return Json(new { found = false });
            }

            return Json(new
            {
                found = true,
                fullName = member.Value.fullName,
                points = member.Value.points
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PosUpdate([FromForm] string barcode, [FromForm] int qty, [FromQuery] string? search, [FromQuery] string? tabId)
        {
            try
            {
                var currentTabId = NormalizePosTabId(tabId);
                var cart = GetCartFromSession(currentTabId);

                var line = cart.Lines.FirstOrDefault(x => string.Equals(x.Code, barcode, StringComparison.OrdinalIgnoreCase));
                if (line != null)
                {
                    line.Qty = Math.Max(0, qty);
                    if (line.Qty == 0)
                    {
                        cart.Lines.Remove(line);
                    }
                }

                SetCartToSession(currentTabId, cart);
                if (IsSpaFragmentRequest())
                {
                    return await PosViewAsync(search, currentTabId);
                }

                return RedirectToAction(nameof(Pos), new { search, tabId = currentTabId });
            }
            catch (Exception ex)
            {
                return Content("CUSTOM_ERROR_POSUPDATE_" + ex.Message + " | " + ex.StackTrace, "text/plain");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PosRemove([FromForm] string barcode, [FromQuery] string? search, [FromQuery] string? tabId)
        {
            var currentTabId = NormalizePosTabId(tabId);
            var cart = GetCartFromSession(currentTabId);
            cart.Lines.RemoveAll(x => string.Equals(x.Code, barcode, StringComparison.OrdinalIgnoreCase));
            SetCartToSession(currentTabId, cart);
            if (IsSpaFragmentRequest())
            {
                return await PosViewAsync(search, currentTabId);
            }

            return RedirectToAction(nameof(Pos), new { search, tabId = currentTabId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PosCheckout(
            [FromForm] string? customerPhone,
            [FromForm] bool usePoints,
            [FromForm] decimal cash,
            [FromForm] string? paymentMethod,
            [FromQuery] string? search,
            [FromQuery] string? tabId)
        {
            var currentTabId = NormalizePosTabId(tabId);
            var cart = GetCartFromSession(currentTabId);
            var subtotal = cart.Lines.Sum(x => x.Amount);
            if (subtotal <= 0)
            {
                ViewData["Title"] = "POS (Point of Sale)";
                var emptyVm = new PosPageVm
                {
                    CurrentTabId = currentTabId,
                    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    Products = await _posRepo.SearchProductsAsync(string.Empty, null, null),
                    Cart = cart,
                    Error = "Cart is empty."
                };
                return View("Pos", emptyVm);
            }

            decimal taxRate = 0.1m;
            var tax = subtotal * taxRate;
            const decimal POINT_RATE = 1m;
            int? customerId = null;
            int points = 0;

            var member = await _posRepo.FindMemberCustomerByPhoneAsync(customerPhone);
            if (member != null)
            {
                customerId = member.Value.customerId;
                points = member.Value.points;
            }

            var discount = 0m;
            int pointsUsed = 0;
            if (usePoints && customerId.HasValue)
            {
                var maxDiscount = points * POINT_RATE;
                discount = Math.Min(maxDiscount, subtotal + tax);
                pointsUsed = (int)Math.Ceiling(discount / POINT_RATE);
            }

            var grandTotal = subtotal + tax - discount;
            var pm = string.IsNullOrWhiteSpace(paymentMethod) ? "cash" : paymentMethod.Trim().ToLowerInvariant();

            if (pm == "cash" && cash < grandTotal)
            {
                ViewData["Title"] = "POS (Point of Sale)";
                var vm = new PosPageVm
                {
                    CurrentTabId = currentTabId,
                    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    Products = await _posRepo.SearchProductsAsync(string.Empty, null, null),
                    Cart = cart,
                    Error = $"Not enough cash. Need {grandTotal:N0}, got {cash:N0}."
                };
                return View("Pos", vm);
            }

            if (User.IsInRole("Admin")) return Forbid();

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userId = string.IsNullOrEmpty(userIdStr) ? 1 : int.Parse(userIdStr);
            var orderId = await _posRepo.SaveOrderAsync(cart.Lines, userId, customerId, grandTotal, pointsUsed);

            if (pm == "cash")
            {
                TempData["PosCash"] = cash;
                TempData["PosChange"] = cash - grandTotal;

                TempData.Remove("PosPaymentMethod");
                TempData.Remove("PosPaymentQrValue");
            }
            else
            {
                TempData["PosPaymentMethod"] = "vnpay";
                TempData["PosPaymentQrValue"] = $"VNPAY|ORDER|{orderId}|AMOUNT|{grandTotal}";

                TempData.Remove("PosCash");
                TempData.Remove("PosChange");
            }

    
            HttpContext.Session.Remove(GetPosCartSessionKey(currentTabId));

            if (IsSpaFragmentRequest())
            {
                return Json(new { redirect = Url.Action(nameof(PosReceipt), new { orderId }) });
            }

            return RedirectToAction(nameof(PosReceipt), new { orderId });
        }

        [HttpGet]
        public async Task<IActionResult> PosCancel(string? search, string? tabId)
        {
            var currentTabId = NormalizePosTabId(tabId);
            
            HttpContext.Session.Remove(GetPosCartSessionKey(currentTabId));
            TempData.Remove("PosCash");
            TempData.Remove("PosChange");
            TempData.Remove("PosPaymentMethod");
            TempData.Remove("PosPaymentQrValue");

            if (IsSpaFragmentRequest())
            {
                return await PosViewAsync(search, currentTabId);
            }

            return RedirectToAction(nameof(Pos), new { search, tabId = currentTabId });
        }

        [HttpGet]
        public async Task<IActionResult> PosReceipt(int orderId)
        {
            var receipt = await _posRepo.GetReceiptAsync(orderId);
            if (receipt == null)
            {
                return RedirectToAction(nameof(Pos));
            }

            if (TempData["PosCash"] is decimal cash)
            {
                receipt.Cash = cash;
            }
            if (TempData["PosChange"] is decimal change)
            {
                receipt.Change = change;
            }

            if (TempData["PosPaymentMethod"] is string pm && !string.IsNullOrWhiteSpace(pm))
            {
                receipt.PaymentMethod = pm;
            }
            if (TempData["PosPaymentQrValue"] is string qr && !string.IsNullOrWhiteSpace(qr))
            {
                receipt.PaymentQrValue = qr;
            }

            return View("PosReceipt", receipt);
        }

        [HttpGet]
        public async Task<IActionResult> Products(string? search, int? categoryId, string? ageRange, int? id)
        {
            ViewData["Title"] = "Products";

            var (items, categories, ages) = await _productsRepo.GetProductsAsync(search, categoryId, ageRange);
            ProductVm? selected = null;
            if (id is > 0)
            {
                selected = await _productsRepo.GetByIdAsync(id.Value);
            }

            var vm = new ProductsPageVm
            {
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                CategoryId = categoryId,
                AgeRange = string.IsNullOrWhiteSpace(ageRange) ? null : ageRange.Trim(),
                Categories = categories,
                AgeRanges = ages,
                Products = items,
                SelectedProduct = selected
            };

            if (selected != null)
            {
                vm.Form = new ProductFormVm
                {
                    ProductId = selected.Id,
                    Barcode = selected.Barcode,
                    Name = selected.Name,
                    CategoryName = selected.CategoryName,
                    AgeRange = selected.AgeRange,
                    ImportPrice = selected.ImportPrice,
                    SellPrice = selected.SellPrice,
                    ExistingImagePath = selected.ImagePath,
                    BarcodeImagePath = selected.BarcodeImagePath
                };
            }
            else
            {
                vm.Form = new ProductFormVm
                {
                    ProductId = null,
                    Barcode = GenerateRandomEan13(),
                    Name = "",
                    CategoryName = categories.Count > 0 ? categories[0].Name : "",
                    AgeRange = ages.Count > 0 ? ages[0] : "",
                    ImportPrice = 0,
                    SellPrice = 0,
                    ExistingImagePath = null,
                    BarcodeImagePath = null
                };
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProductsSave(
            [FromForm] ProductFormVm form,
            [FromQuery] string? search,
            [FromQuery] int? categoryId,
            [FromQuery] string? ageRange)
        {
            ViewData["Title"] = "Products";

            try
            {
                if (string.IsNullOrWhiteSpace(form.Barcode))
                    form.Barcode = GenerateRandomEan13();

                form.Name = (form.Name ?? "").Trim();
                form.CategoryName = (form.CategoryName ?? "").Trim();
                form.AgeRange = (form.AgeRange ?? "").Trim();

                if (string.IsNullOrWhiteSpace(form.Name) || string.IsNullOrWhiteSpace(form.CategoryName) || string.IsNullOrWhiteSpace(form.AgeRange))
                {
                    throw new InvalidOperationException("Missing required fields.");
                }

                await SaveProductImageIfAny(form);

                var savedId = await _productsRepo.SaveAsync(form);

                return RedirectToAction(nameof(Products), new
                {
                    id = savedId,
                    search,
                    categoryId,
                    ageRange
                });
            }
            catch (Exception ex)
            {
                
                var page = await RebuildProductsPageAsync(search, categoryId, ageRange, form.ProductId);
                page.Error = ex.Message;
                return View("Products", page);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProductsDelete(
            [FromForm] int productId,
            [FromQuery] string? search,
            [FromQuery] int? categoryId,
            [FromQuery] string? ageRange)
        {
            ViewData["Title"] = "Products";

            await _productsRepo.DeleteAsync(productId);

            return RedirectToAction(nameof(Products), new
            {
                id = (int?)null,
                search,
                categoryId,
                ageRange
            });
        }

        [HttpGet]
        public async Task<IActionResult> StockCheck(int? id, bool create = false)
        {
            ViewData["Title"] = "Inventory Audit";
            await _inventoryRepo.EnsureSchemaAsync();
            
            var history = await _inventoryRepo.GetStockAuditHistoryAsync();
            var vm = new StockCheckPageVm
            {
                History = history,
                Date = DateTime.Today
            };

            if (id.HasValue)
            {
                vm.SelectedAudit = history.FirstOrDefault(h => h.AuditId == id.Value);
                if (vm.SelectedAudit != null)
                {
                    vm.SelectedLines = await _inventoryRepo.GetStockAuditLinesAsync(id.Value);
                }
            }
            else if (create)
            {
                vm.Items = await _inventoryRepo.GetStockCheckDataAsync();
            }

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SaveStockCheck([FromBody] StockAuditSaveVm model)
        {
            if (model == null || model.Items == null || model.Items.Count == 0)
            {
                return Json(new { success = false, message = "No data to save." });
            }

            try
            {
                int? userId = null;
                var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out var id)) userId = id;

                var auditId = await _inventoryRepo.SaveStockAuditAsync(model, userId);
                return Json(new { success = true, message = $"Stock check saved successfully! (Audit ID: {auditId})", auditId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving audit: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Inventory(
            int? id,
            DateTime? from,
            DateTime? to,
            string? type,
            string? q,
            string? section)
        {
            ViewData["Title"] = "Inventory";

            await _inventoryRepo.EnsureSchemaAsync();

            var start = (from ?? DateTime.Today.AddDays(-30)).Date;
            var end = (to ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

            var sec = string.IsNullOrWhiteSpace(section) ? "purchase" : section.Trim().ToLowerInvariant();
            if (sec != "purchase" && sec != "sales")
            {
                sec = "purchase";
            }

            var effectiveType = sec == "sales" ? "OUT" : "IN";

            var movements = await _inventoryRepo.GetMovementsAsync(start, end, effectiveType, q);

            StockMovementListVm? selected = null;
            List<StockMovementLineVm> selectedLines = new();
            if (id is > 0)
            {
                var (header, lines) = await _inventoryRepo.GetMovementAsync(id.Value);
                selected = header;
                selectedLines = lines;
            }

            if (id is > 0 && selected != null &&
                !string.Equals(selected.MovementType, effectiveType, StringComparison.OrdinalIgnoreCase))
            {
                var correctSec = string.Equals(selected.MovementType, "OUT", StringComparison.OrdinalIgnoreCase)
                    ? "sales"
                    : "purchase";
                return RedirectToAction(nameof(Inventory), new
                {
                    id,
                    from = start.ToString("yyyy-MM-dd"),
                    to = (to ?? DateTime.Today).Date.ToString("yyyy-MM-dd"),
                    section = correctSec,
                    q
                });
            }

            var products = await _inventoryRepo.GetProductsForDropdownAsync();

            var vm = new InventoryPageVm
            {
                Section = sec,
                Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
                TypeFilter = effectiveType,
                From = start,
                To = (to ?? DateTime.Today).Date,
                Movements = movements,
                SelectedMovement = selected,
                Products = products,
                Form = selected != null
                    ? new StockMovementFormVm
                    {
                        MovementId = selected.MovementId,
                        MovementType = selected.MovementType,
                        CreatedAt = selected.CreatedAt,
                        Note = selected.Note,
                        AffectsStock = selected.AffectsStock,
                        Lines = selectedLines.Select(l => new StockMovementLineVm
                        {
                            ProductId = l.ProductId,
                            Quantity = l.Quantity,
                            UnitPrice = l.UnitPrice
                        }).ToList()
                    }
                    : new StockMovementFormVm
                    {
                        MovementType = effectiveType,
                        CreatedAt = DateTime.Now,
                        Note = "",
                        AffectsStock = true,
                        Lines = new List<StockMovementLineVm>
                        {
                            new StockMovementLineVm { ProductId = products.Count > 0 ? products[0].ProductId : 0, Quantity = 1, UnitPrice = 0 }
                        }
                    }
            };

            if (vm.Form.Lines.Count == 0 && products.Count > 0)
            {
                vm.Form.Lines.Add(new StockMovementLineVm { ProductId = products[0].ProductId, Quantity = 1, UnitPrice = 0 });
            }

            var padTo = 10;
            if (products.Count > 0)
            {
                while (vm.Form.Lines.Count < padTo)
                {
                    vm.Form.Lines.Add(new StockMovementLineVm
                    {
                        ProductId = products[0].ProductId,
                        Quantity = 0,
                        UnitPrice = 0
                    });
                }
            }

            return View("Inventory", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InventorySave(
            [FromForm] StockMovementFormVm form,
            [FromQuery] int? id,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? type,
            [FromQuery] string? q,
            [FromQuery] string? section)
        {
            ViewData["Title"] = "Inventory";
            await _inventoryRepo.EnsureSchemaAsync();

            var createdByUserId = 1;

            try
            {
                int savedId;
                if (form.MovementId.HasValue)
                {
                    await _inventoryRepo.UpdateMovementAsync(form, createdByUserId);
                    savedId = form.MovementId.Value;
                }
                else
                {
                    savedId = await _inventoryRepo.InsertMovementAsync(form, createdByUserId);
                }

                var secRedirect = form.MovementType.Equals("OUT", StringComparison.OrdinalIgnoreCase) ? "sales" : "purchase";
                return RedirectToAction(nameof(Inventory), new
                {
                    id = savedId,
                    from = from?.ToString("yyyy-MM-dd"),
                    to = to?.ToString("yyyy-MM-dd"),
                    section = secRedirect,
                    q
                });
            }
            catch (Exception ex)
            {
                var secErr = string.IsNullOrWhiteSpace(section) ? "purchase" : section.Trim().ToLowerInvariant();
                if (secErr != "purchase" && secErr != "sales") secErr = "purchase";
                var typeErr = secErr == "sales" ? "OUT" : "IN";

                var products = await _inventoryRepo.GetProductsForDropdownAsync();
                var movements = await _inventoryRepo.GetMovementsAsync(
                    (from ?? DateTime.Today.AddDays(-30)).Date,
                    (to ?? DateTime.Today).Date.AddDays(1).AddTicks(-1),
                    typeErr,
                    q);

                return View("Inventory", new InventoryPageVm
                {
                    Section = secErr,
                    Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
                    TypeFilter = secErr == "sales" ? "OUT" : "IN",
                    From = (from ?? DateTime.Today.AddDays(-30)).Date,
                    To = (to ?? DateTime.Today).Date,
                    Movements = movements,
                    SelectedMovement = null,
                    Products = products,
                    Form = form,
                    Error = ex.Message
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InventoryDelete(
            [FromForm] int movementId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string? type,
            [FromQuery] string? q,
            [FromQuery] string? section)
        {
            ViewData["Title"] = "Inventory";
            await _inventoryRepo.EnsureSchemaAsync();
            await _inventoryRepo.DeleteMovementAsync(movementId);

            var sec = string.IsNullOrWhiteSpace(section) ? "purchase" : section.Trim().ToLowerInvariant();
            if (sec != "purchase" && sec != "sales") sec = "purchase";

            return RedirectToAction(nameof(Inventory), new
            {
                id = (int?)null,
                from = from?.ToString("yyyy-MM-dd"),
                to = to?.ToString("yyyy-MM-dd"),
                section = sec,
                q
            });
        }

        [HttpGet]
        public async Task<IActionResult> Customers(int? id, string? search)
        {
            ViewData["Title"] = "Customers";

            var customers = await _customersRepo.GetCustomersAsync(search);
            CustomerVm? selected = null;
            if (id is > 0)
            {
                selected = await _customersRepo.GetByIdAsync(id.Value);
            }
            selected ??= customers.Count > 0 ? customers[0] : null;

            var vm = new CustomersPageVm
            {
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                Customers = customers,
                SelectedCustomer = selected,
                Orders = selected != null ? await _customersRepo.GetOrdersAsync(selected.Id) : new List<CustomerOrderVm>()
            };

            return View("Customers", vm);
        }

        [HttpGet]
        public async Task<IActionResult> CustomerDetailsData(int id)
        {
            var orders = await _customersRepo.GetOrdersAsync(id);
            return Json(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Reports(int? id, DateTime? from, DateTime? to, string? search, string? status, string? paymentMethod)
        {
            ViewData["Title"] = "Sales History";

            var start = (from ?? DateTime.Today).Date;
            var end = to ?? DateTime.Today;
    
            end = end.Date.AddDays(1).AddTicks(-1);

            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
            var normalizedPaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? null : paymentMethod.Trim();
            var orders = await _reportsRepo.GetOrdersAsync(start, end, search, normalizedStatus, normalizedPaymentMethod);

            OrderVm? selected = null;
            List<OrderDetailVm> details = new();

            if (id is > 0)
            {
                var (order, orderDetails) = await _reportsRepo.GetOrderDetailsAsync(id.Value);
                selected = order;
                details = orderDetails;
            }

            if (selected != null && details.Count == 0)
            {
                var (order, orderDetails) = await _reportsRepo.GetOrderDetailsAsync(selected.OrderId);
                details = orderDetails;
            }

            var vm = new ReportsPageVm
            {
                From = start,
                To = end,
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                Status = normalizedStatus,
                PaymentMethod = normalizedPaymentMethod,
                Orders = orders,
                SelectedOrder = selected,
                OrderDetails = details
            };

            return View("Reports", vm);
        }

        [HttpGet]
        public async Task<IActionResult> Staff(int? id, string? search, int sortIndex)
        {
            ViewData["Title"] = "Staff";
            await _staffRepo.EnsureSchemaAsync();

            var employees = await _staffRepo.GetEmployeesAsync(search, sortIndex);
            EmployeeVm? selected = null;
            if (id is > 0)
            {
                selected = await _staffRepo.GetByIdAsync(id.Value);
            }
            var vm = new StaffPageVm
            {
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                SortIndex = sortIndex,
                Employees = employees,
                SelectedEmployee = selected,
                Form = selected != null
                    ? new EmployeeFormVm
                    {
                        EmployeeId = selected.Id,
                        FullName = selected.FullName,
                        Gender = selected.Gender,
                        BirthDate = selected.BirthDate,
                        Email = selected.Email,
                        Address = selected.Address,
                        Phone = selected.Phone,
                        PositionTitle = selected.PositionTitle,
                        ExistingImagePath = selected.ImagePath,
                        ImagePath = selected.ImagePath
                    }
                    : new EmployeeFormVm()
            };

            return View("Staff", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StaffSave(
            [FromForm] EmployeeFormVm form,
            [FromQuery] string? search,
            [FromQuery] int sortIndex,
            [FromQuery] int? id)
        {
            ViewData["Title"] = "Staff";
            await _staffRepo.EnsureSchemaAsync();

            form.FullName = (form.FullName ?? "").Trim();
            form.Email = (form.Email ?? "").Trim();
            form.Address = (form.Address ?? "").Trim();
            form.Phone = (form.Phone ?? "").Trim();
            form.Gender = (form.Gender ?? "Male").Trim();
            form.PositionTitle = (form.PositionTitle ?? "").Trim();

            if (string.IsNullOrWhiteSpace(form.FullName))
            {
                ModelState.AddModelError("", "Full name is required.");
            }
            if (string.IsNullOrWhiteSpace(form.Phone))
            {
                ModelState.AddModelError("", "Phone is required.");
            }

            if (!ModelState.IsValid)
            {
                return await Staff(id, search, sortIndex);
            }

            await SaveStaffImageIfAny(form);
            if (string.IsNullOrWhiteSpace(form.ImagePath))
            {
                form.ImagePath = (form.ExistingImagePath ?? "").Trim();
            }

            var savedId = await _staffRepo.UpsertByPhoneAsync(form);
            return RedirectToAction(nameof(Staff), new { id = savedId, search, sortIndex });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StaffDelete(
            [FromForm] int employeeId,
            [FromQuery] string? search,
            [FromQuery] int sortIndex)
        {
            ViewData["Title"] = "Staff";
            await _staffRepo.EnsureSchemaAsync();
            await _staffRepo.DeleteAsync(employeeId);
            return RedirectToAction(nameof(Staff), new { id = (int?)null, search, sortIndex });
        }

        private async Task SaveStaffImageIfAny(EmployeeFormVm form)
        {
            if (form.ImageFile == null || form.ImageFile.Length <= 0)
            {
                return;
            }

            var uploadsDir = Path.Combine(_env.WebRootPath ?? "", "uploads", "staff");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            var ext = Path.GetExtension(form.ImageFile.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
            ext = ext.Trim('.').Length > 5 ? ".png" : ext;

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsDir, fileName);
            await using (var fs = System.IO.File.Create(fullPath))
            {
                await form.ImageFile.CopyToAsync(fs);
            }

            form.ImagePath = $"/uploads/staff/{fileName}";
        }

        private async Task SaveProductImageIfAny(ProductFormVm form)
        {
            if (form.ImageFile == null || form.ImageFile.Length <= 0)
            {
                return;
            }

            var uploadsDir = Path.Combine(_env.WebRootPath ?? "", "uploads", "products");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            var ext = Path.GetExtension(form.ImageFile.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
            ext = ext.Trim('.').Length > 5 ? ".png" : ext;

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsDir, fileName);
            await using (var fs = System.IO.File.Create(fullPath))
            {
                await form.ImageFile.CopyToAsync(fs);
            }

            form.ExistingImagePath = $"/uploads/products/{fileName}";
        }

        private async Task<ProductsPageVm> RebuildProductsPageAsync(string? search, int? categoryId, string? ageRange, int? selectedId)
        {
            var (items, categories, ages) = await _productsRepo.GetProductsAsync(search, categoryId, ageRange);
            ProductVm? selected = null;
            if (selectedId is > 0)
            {
                selected = await _productsRepo.GetByIdAsync(selectedId.Value);
            }

            var vm = new ProductsPageVm
            {
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                CategoryId = categoryId,
                AgeRange = string.IsNullOrWhiteSpace(ageRange) ? null : ageRange.Trim(),
                Categories = categories,
                AgeRanges = ages,
                Products = items,
                SelectedProduct = selected
            };

            vm.Form = selected != null
                ? new ProductFormVm
                {
                    ProductId = selected.Id,
                    Barcode = selected.Barcode,
                    Name = selected.Name,
                    CategoryName = selected.CategoryName,
                    AgeRange = selected.AgeRange,
                    ImportPrice = selected.ImportPrice,
                    SellPrice = selected.SellPrice,
                    ExistingImagePath = selected.ImagePath
                }
                : new ProductFormVm
                {
                    ProductId = null,
                    Barcode = GenerateRandomEan13(),
                    Name = "",
                    CategoryName = categories.Count > 0 ? categories[0].Name : "",
                    AgeRange = ages.Count > 0 ? ages[0] : "",
                    ImportPrice = 0,
                    SellPrice = 0,
                    ExistingImagePath = null
                };

            return vm;
        }

        private const string PosCartSessionKey = "pos_cart";

        private bool IsSpaFragmentRequest()
        {
            return string.Equals(Request.Headers["X-SPA-Fragment"].ToString(), "1", StringComparison.Ordinal);
        }

        private async Task<PosPageVm> BuildPosVmAsync(string? search, string? tabId)
        {
            var currentTabId = NormalizePosTabId(tabId);
            var cart = GetCartFromSession(currentTabId);
            var products = await _posRepo.SearchProductsAsync(string.Empty, null, null);
            var error = TempData["PosError"] as string;
            var vm = new PosPageVm
            {
                CurrentTabId = currentTabId,
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                Products = products,
                Cart = cart,
                Error = error
            };
            return vm;
        }

        private async Task<IActionResult> PosViewAsync(string? search, string? tabId)
        {
            ViewData["Title"] = "POS (Point of Sale)";
            var vm = await BuildPosVmAsync(search, tabId);
            return View("Pos", vm);
        }

        private PosCartVm GetCartFromSession(string? tabId)
        {
            var json = HttpContext.Session.GetString(GetPosCartSessionKey(tabId));
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PosCartVm();
            }

            try
            {
                return JsonSerializer.Deserialize<PosCartVm>(json) ?? new PosCartVm();
            }
            catch
            {
                return new PosCartVm();
            }
        }

        private void SetCartToSession(string? tabId, PosCartVm cart)
        {
            var payload = JsonSerializer.Serialize(cart ?? new PosCartVm());
            HttpContext.Session.SetString(GetPosCartSessionKey(tabId), payload);
        }

        private static string NormalizePosTabId(string? tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId))
            {
                return "default";
            }

            var safe = new string(tabId.Trim().Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray());
            if (string.IsNullOrWhiteSpace(safe))
            {
                return "default";
            }

            if (safe.Length > 40)
            {
                safe = safe[..40];
            }

            return safe;
        }

        private static string GetPosCartSessionKey(string? tabId)
        {
            return $"{PosCartSessionKey}_{NormalizePosTabId(tabId)}";
        }

        private static string GenerateRandomEan13()
        {
            var rnd = Random.Shared;
            var randomPart = rnd.Next(100000000, 999999999).ToString(); 
            var code12 = "893" + randomPart; 
            var checkDigit = ComputeEan13CheckDigit(code12);
            return code12 + checkDigit;
        }

        private static string ComputeEan13CheckDigit(string first12Digits)
        {
            if (string.IsNullOrWhiteSpace(first12Digits) || first12Digits.Length != 12)
            {
                return "0";
            }

            var sum = 0;
            for (var i = 0; i < 12; i++)
            {
                var digit = first12Digits[i] - '0';
                sum += (i % 2 == 0) ? digit : digit * 3;
            }

            var mod = sum % 10;
            var check = (10 - mod) % 10;
            return check.ToString();
        }
    }
}

