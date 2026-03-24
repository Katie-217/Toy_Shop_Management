# Children's Toy Shop Management Software

Ung dung web quan ly cua hang do choi tre em, xay dung bang ASP.NET Core MVC va SQL Server. He thong ho tro quan ly san pham, kho, ban hang tai quay (POS), khach hang, nhan su va bao cao lich su ban hang.

## 1) Tong quan he thong

Muc tieu cua he thong la gop cac nghiep vu cua cua hang vao mot giao dien thong nhat:

- Quan ly danh muc san pham va thong tin hang hoa.
- Theo doi nhap/xuat kho va bien dong ton.
- Ban hang nhanh tai quay voi gio hang theo tung tab POS.
- Luu lich su giao dich, thong tin thanh toan va chi tiet don.
- Quan ly thong tin khach hang thanh vien va diem tich luy.
- Quan ly nhan vien phuc vu van hanh cua hang.

## 2) Cau truc project hien co

```text
.
|-- Controllers/
|   |-- PortalController.cs
|   |-- HomeController.cs
|   `-- HealthController.cs
|
|-- Data/
|   |-- SqlConnectionFactory.cs
|   |-- ProductsRepository.cs
|   |-- InventoryRepository.cs
|   |-- PosRepository.cs
|   |-- CustomersRepository.cs
|   |-- StaffRepository.cs
|   |-- ReportsRepository.cs
|   `-- DashboardRepository.cs
|
|-- Models/
|   |-- Dashboard/
|   |-- Products/
|   |-- Inventory/
|   |-- POS/
|   |-- Customers/
|   |-- Staff/
|   |-- Reports/
|   `-- Shared/
|
|-- Views/
|   |-- Portal/
|   |   |-- Dashboard.cshtml
|   |   |-- Pos.cshtml
|   |   |-- PosReceipt.cshtml
|   |   |-- Products.cshtml
|   |   |-- Inventory.cshtml
|   |   |-- Customers.cshtml
|   |   |-- Staff.cshtml
|   |   `-- Reports.cshtml
|   `-- Shared/
|       |-- _ToyLayout.cshtml
|       |-- _ToyLayoutFragment.cshtml
|       `-- _BillReceipt.cshtml
|
|-- wwwroot/
|   |-- css/
|   |   `-- toyshop-dark.css
|   `-- uploads/
|
|-- database/
|   `-- seed_march_2026.sql
|
|-- Program.cs
|-- appsettings.json
`-- toy_shop_management_software.csproj
```

### Vai tro tung thu muc/chinh

- `Controllers/`: dieu huong request, goi repository, tra ve view.
- `Data/`: truy van SQL Server theo tung module nghiep vu.
- `Models/`: ViewModel dung de truyen du lieu giua Data -> View.
- `Views/`: giao dien Razor cho tung man hinh va layout tong.
- `wwwroot/`: tai nguyen tinh (CSS, JS, hinh anh upload).
- `database/`: script SQL de seed va dong bo du lieu demo.
- `Program.cs`: dang ky DI, Session, middleware, route mac dinh.
- `appsettings.json`: cau hinh chuoi ket noi SQL Server.

## 3) Chuc nang tung tab tren menu

Menu hien tai nam trong sidebar cua layout chung:

- `Dashboard`
  - Tong hop KPI tong quan cua cua hang (doanh thu, so lieu nhanh theo ngay/ky).

- `POS`
  - Ban hang tai quay.
  - Quet/nhap ma, tim san pham, cap nhat gio hang.
  - Ho tro checkout tien mat/VNPay, in/tra cuu hoa don.
  - Luu gio hang theo tung `tabId` trong Session.

- `Inventory` (nhom menu con)
  - `Product Entry`: vao trang san pham de tao/sua/xoa thong tin hang hoa.
  - `Stock In`: quan ly phieu nhap kho (`MovementType = IN`).
  - `Stock Check`: hien tai tro ve trang `Products` de doi chieu ton/thong tin san pham.
  - `Returns`: quan ly phieu xuat/tra hang (`MovementType = OUT`).

- `Sales History`
  - Loc danh sach don theo ngay, trang thai, phuong thuc thanh toan, tu khoa.
  - Xem chi tiet tung don va cac dong san pham trong don.

- `Customers`
  - Xem/tim kiem danh sach khach hang.
  - Xem lich su don cua khach va thong tin thanh vien/diem.

- `Staff`
  - Quan ly thong tin nhan vien (ho ten, lien he, vi tri, hinh anh).
  - Ho tro them/sua/xoa theo nghiep vu quan ly nhan su cua cua hang.

## 4) Cach chay project

### Yeu cau

- .NET SDK 8.0+
- SQL Server (khuyen nghi `SQLEXPRESS`)
- SQL Server Management Studio (SSMS) hoac `sqlcmd`

### Cau hinh ket noi DB

Cap nhat `ConnectionStrings:DefaultConnection` trong `appsettings.json` cho dung may cua ban. Gia tri mac dinh:

```json
"Server=localhost\\SQLEXPRESS;Database=ToyshopDB_MVC;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

### Chay ung dung

Tai thu muc goc project:

```bash
dotnet restore
dotnet run
```

Mo trinh duyet voi URL duoc in ra terminal (thuong la `https://localhost:xxxx`).

## 5) Cach chay script seed de lay data demo

File script: `database/seed_march_2026.sql`

Script se:

- Bo sung cot/ban neu thieu (schema migration nhe).
- Lam sach du lieu giao dich cu de tao bo du lieu demo on dinh.
- Seed danh muc, nha cung cap, 30 san pham, 15 nhan vien, 30 khach hang.
- Tao du lieu don hang trong thang 03/2026.
- Cap nhat ton kho, diem khach hang.
- Tao phieu nhap kho va phieu tra hang mau.

### Cach 1: Chay bang SSMS

1. Mo SSMS, ket noi SQL Server.
2. Chon database `ToyshopDB_MVC` (hoac DB ban dang dung).
3. Mo file `database/seed_march_2026.sql`.
4. Bam `Execute`.
5. Chay lai ung dung va refresh trang de thay du lieu moi.

### Cach 2: Chay bang sqlcmd (PowerShell)

```powershell
sqlcmd -S "localhost\SQLEXPRESS" -d "ToyshopDB_MVC" -E -i "database\seed_march_2026.sql"
```

Giai thich nhanh:

- `-S`: ten SQL Server instance
- `-d`: ten database
- `-E`: dang nhap Windows Authentication
- `-i`: duong dan file script

Neu ban dung SQL login thay vi Windows Authentication:

```powershell
sqlcmd -S "localhost\SQLEXPRESS" -d "ToyshopDB_MVC" -U "sa" -P "your_password" -i "database\seed_march_2026.sql"
```

---
