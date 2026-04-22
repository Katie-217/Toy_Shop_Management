USE ToyshopDB_MVC;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    /* ------------------------------------------------------------
       1) Ensure schema for requested data fields
    ------------------------------------------------------------ */
    IF COL_LENGTH('dbo.Products', 'Description') IS NULL
        ALTER TABLE dbo.Products ADD [Description] NVARCHAR(500) NULL;
    IF COL_LENGTH('dbo.Products', 'ImagePath') IS NULL
        ALTER TABLE dbo.Products ADD [ImagePath] NVARCHAR(260) NULL;

    IF COL_LENGTH('dbo.Customers', 'Points') IS NULL
        ALTER TABLE dbo.Customers ADD [Points] INT NOT NULL CONSTRAINT DF_Customers_Points DEFAULT(0);
    IF COL_LENGTH('dbo.Customers', 'IsMember') IS NULL
        ALTER TABLE dbo.Customers ADD [IsMember] BIT NOT NULL CONSTRAINT DF_Customers_IsMember DEFAULT(1);

    IF COL_LENGTH('dbo.Employees', 'PositionTitle') IS NULL
        ALTER TABLE dbo.Employees ADD [PositionTitle] NVARCHAR(120) NULL;
    IF COL_LENGTH('dbo.Employees', 'ImagePath') IS NULL
        ALTER TABLE dbo.Employees ADD [ImagePath] NVARCHAR(260) NULL;

    IF COL_LENGTH('dbo.Orders', 'Status') IS NULL
        ALTER TABLE dbo.Orders ADD [Status] NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.Orders', 'PaymentMethod') IS NULL
        ALTER TABLE dbo.Orders ADD [PaymentMethod] NVARCHAR(50) NULL;

    IF OBJECT_ID('dbo.StockMovementHeaders', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.StockMovementHeaders(
            MovementID INT IDENTITY(1,1) PRIMARY KEY,
            MovementType NVARCHAR(10) NOT NULL,
            AffectsStock BIT NOT NULL,
            Note NVARCHAR(255) NULL,
            CreatedByUserID INT NULL,
            CreatedAt DATETIME NOT NULL DEFAULT(GETDATE())
        );
    END;

    IF OBJECT_ID('dbo.StockMovementLines', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.StockMovementLines(
            LineID INT IDENTITY(1,1) PRIMARY KEY,
            MovementID INT NOT NULL,
            ProductID INT NOT NULL,
            Quantity INT NOT NULL,
            UnitPrice DECIMAL(18,2) NOT NULL,
            CONSTRAINT FK_StockMovementLines_Headers FOREIGN KEY (MovementID)
                REFERENCES dbo.StockMovementHeaders(MovementID)
                ON DELETE CASCADE
        );
    END;

    IF COL_LENGTH('dbo.StockMovementHeaders', 'ReceiptAttachment') IS NULL
        ALTER TABLE dbo.StockMovementHeaders ADD [ReceiptAttachment] NVARCHAR(260) NULL;
    IF COL_LENGTH('dbo.StockMovementHeaders', 'Reason') IS NULL
        ALTER TABLE dbo.StockMovementHeaders ADD [Reason] NVARCHAR(255) NULL;
    IF COL_LENGTH('dbo.StockMovementHeaders', 'EvidenceImagePath') IS NULL
        ALTER TABLE dbo.StockMovementHeaders ADD [EvidenceImagePath] NVARCHAR(260) NULL;
    IF COL_LENGTH('dbo.StockMovementHeaders', 'PreparedBy') IS NULL
        ALTER TABLE dbo.StockMovementHeaders ADD [PreparedBy] NVARCHAR(120) NULL;
    IF COL_LENGTH('dbo.StockMovementHeaders', 'ApprovedBy') IS NULL
        ALTER TABLE dbo.StockMovementHeaders ADD [ApprovedBy] NVARCHAR(120) NULL;
    IF COL_LENGTH('dbo.StockMovementHeaders', 'RefundAmount') IS NULL
        ALTER TABLE dbo.StockMovementHeaders ADD [RefundAmount] DECIMAL(18,2) NULL;
    IF COL_LENGTH('dbo.StockMovementHeaders', 'RefundMethod') IS NULL
        ALTER TABLE dbo.StockMovementHeaders ADD [RefundMethod] NVARCHAR(30) NULL;

    /* ------------------------------------------------------------
       2) Clean transactional data for deterministic demo seed
    ------------------------------------------------------------ */
    IF OBJECT_ID('dbo.OrderDetails', 'U') IS NOT NULL DELETE FROM dbo.OrderDetails;
    IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL DELETE FROM dbo.Orders;
    IF OBJECT_ID('dbo.StockMovementLines', 'U') IS NOT NULL DELETE FROM dbo.StockMovementLines;
    IF OBJECT_ID('dbo.StockMovementHeaders', 'U') IS NOT NULL DELETE FROM dbo.StockMovementHeaders;

    IF OBJECT_ID('dbo.Customers', 'U') IS NOT NULL DELETE FROM dbo.Customers;
    IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL UPDATE dbo.Products SET IsActive = 0;

    IF OBJECT_ID('dbo.Customers', 'U') IS NOT NULL DBCC CHECKIDENT ('dbo.Customers', RESEED, 0) WITH NO_INFOMSGS;

    /* ------------------------------------------------------------
       3) Categories / suppliers
    ------------------------------------------------------------ */
    IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = N'Building Toys') INSERT INTO dbo.Categories(Name) VALUES (N'Building Toys');
        IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = N'Educational') INSERT INTO dbo.Categories(Name) VALUES (N'Educational');
        IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = N'Role Play') INSERT INTO dbo.Categories(Name) VALUES (N'Role Play');
        IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = N'Outdoor') INSERT INTO dbo.Categories(Name) VALUES (N'Outdoor');
        IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = N'Creative') INSERT INTO dbo.Categories(Name) VALUES (N'Creative');
        IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = N'Board Games') INSERT INTO dbo.Categories(Name) VALUES (N'Board Games');
    END;

    IF OBJECT_ID('dbo.Suppliers', 'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE Name = N'Mattel Vietnam') INSERT INTO dbo.Suppliers(Name) VALUES (N'Mattel Vietnam');
        IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE Name = N'Hasbro APAC') INSERT INTO dbo.Suppliers(Name) VALUES (N'Hasbro APAC');
        IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE Name = N'Lego Distribution') INSERT INTO dbo.Suppliers(Name) VALUES (N'Lego Distribution');
        IF NOT EXISTS (SELECT 1 FROM dbo.Suppliers WHERE Name = N'Local Toy Hub') INSERT INTO dbo.Suppliers(Name) VALUES (N'Local Toy Hub');
    END;

    /* ------------------------------------------------------------
       4) Product catalog (30 real products)
    ------------------------------------------------------------ */
    CREATE TABLE #ProductSeed(
        Barcode NVARCHAR(30) PRIMARY KEY,
        ProductName NVARCHAR(200),
        ProductDesc NVARCHAR(500),
        AgeRange NVARCHAR(30),
        ImportPrice DECIMAL(18,2),
        RetailPrice DECIMAL(18,2),
        CategoryName NVARCHAR(120),
        SupplierName NVARCHAR(120),
        InitialQty INT,
        ImagePath NVARCHAR(260)
    );

    INSERT INTO #ProductSeed VALUES
    (N'893850100001',N'LEGO Classic Creative Bricks 10698',N'Large creative brick set with mixed colors for open-ended construction.',N'5+',320000,459000,N'Building Toys',N'Lego Distribution',140,N'/uploads/products/lego-classic-10698.jpg'),
    (N'893850100002',N'LEGO City Fire Station 60320',N'City fire station set with rescue truck and mini figures.',N'6+',890000,1190000,N'Building Toys',N'Lego Distribution',90,N'/uploads/products/lego-city-fire-station-60320.jpg'),
    (N'893850100003',N'LEGO Friends Pet Clinic 41695',N'Role-play pet clinic set for caring and storytelling.',N'4+',410000,579000,N'Building Toys',N'Lego Distribution',110,N'/uploads/products/lego-friends-pet-clinic-41695.jpg'),
    (N'893850100004',N'Hot Wheels 5-Car Pack Track Set',N'Five die-cast cars and short track launcher for speed play.',N'3+',150000,229000,N'Outdoor',N'Mattel Vietnam',180,N'/uploads/products/hotwheels-5car-pack.jpg'),
    (N'893850100005',N'Barbie Dreamhouse Doll Set',N'Fashion doll with mini furniture accessories for role play.',N'3+',380000,529000,N'Role Play',N'Mattel Vietnam',95,N'/uploads/products/barbie-dreamhouse-set.jpg'),
    (N'893850100006',N'UNO Classic Card Game',N'Classic color-number matching card game for family play.',N'7+',45000,79000,N'Board Games',N'Hasbro APAC',220,N'/uploads/products/uno-classic.jpg'),
    (N'893850100007',N'Monopoly Junior Board Game',N'Entry-level property trading game for younger kids.',N'5+',220000,319000,N'Board Games',N'Hasbro APAC',120,N'/uploads/products/monopoly-junior.jpg'),
    (N'893850100008',N'Jenga Wooden Block Tower',N'Balance and dexterity tower game using wooden blocks.',N'6+',160000,249000,N'Board Games',N'Hasbro APAC',150,N'/uploads/products/jenga-wooden.jpg'),
    (N'893850100009',N'Play-Doh Kitchen Creations Set',N'Mold-and-play dough kit with food-themed tools.',N'3+',185000,269000,N'Creative',N'Hasbro APAC',160,N'/uploads/products/playdoh-kitchen.jpg'),
    (N'893850100010',N'Crayola Washable Marker 20 Colors',N'Pack of washable markers for school and drawing.',N'4+',65000,99000,N'Creative',N'Local Toy Hub',260,N'/uploads/products/crayola-markers-20.jpg'),
    (N'893850100011',N'Crayola Coloring Book Animal World',N'Theme coloring book with large kid-friendly pages.',N'3+',28000,45000,N'Creative',N'Local Toy Hub',300,N'/uploads/products/crayola-colorbook-animal.jpg'),
    (N'893850100012',N'Rubiks Cube 3x3 Speed',N'Classic 3x3 puzzle cube for logic and reflex practice.',N'8+',85000,129000,N'Educational',N'Local Toy Hub',170,N'/uploads/products/rubik-3x3.jpg'),
    (N'893850100013',N'VTech Alphabet Learning Laptop',N'Electronic toy laptop for alphabet and basic math.',N'4+',520000,729000,N'Educational',N'Local Toy Hub',80,N'/uploads/products/vtech-learning-laptop.jpg'),
    (N'893850100014',N'Wooden Number Puzzle Board',N'Wooden board puzzle teaching numbers and counting.',N'3+',90000,139000,N'Educational',N'Local Toy Hub',180,N'/uploads/products/wooden-number-puzzle.jpg'),
    (N'893850100015',N'Wooden Alphabet Puzzle Board',N'Alphabet learning board with colorful letters.',N'3+',95000,145000,N'Educational',N'Local Toy Hub',170,N'/uploads/products/wooden-alphabet-puzzle.jpg'),
    (N'893850100016',N'Melissa and Doug Shape Sorter',N'Shape matching box to develop fine motor skills.',N'2+',210000,299000,N'Educational',N'Local Toy Hub',120,N'/uploads/products/shape-sorter.jpg'),
    (N'893850100017',N'Fisher Price Stacking Rings',N'Stacking rings toy for toddlers motor development.',N'1+',140000,215000,N'Educational',N'Mattel Vietnam',190,N'/uploads/products/fp-stacking-rings.jpg'),
    (N'893850100018',N'Nerf Elite Blaster Alpha',N'Foam dart blaster with safe soft darts.',N'8+',390000,549000,N'Outdoor',N'Hasbro APAC',100,N'/uploads/products/nerf-alpha.jpg'),
    (N'893850100019',N'Nerf Foam Dart 30 Pack',N'Refill dart pack compatible with Nerf elite series.',N'8+',70000,109000,N'Outdoor',N'Hasbro APAC',260,N'/uploads/products/nerf-dart-30.jpg'),
    (N'893850100020',N'Scooter Kids 3-Wheel Balance',N'3-wheel beginner scooter with stable deck.',N'4+',620000,829000,N'Outdoor',N'Local Toy Hub',70,N'/uploads/products/scooter-3wheel.jpg'),
    (N'893850100021',N'Soccer Ball Kids Size 3',N'Lightweight soccer ball for kids practice.',N'5+',95000,149000,N'Outdoor',N'Local Toy Hub',210,N'/uploads/products/soccer-ball-size3.jpg'),
    (N'893850100022',N'Basketball Mini Indoor',N'Mini basketball for indoor and backyard play.',N'5+',85000,129000,N'Outdoor',N'Local Toy Hub',190,N'/uploads/products/basketball-mini.jpg'),
    (N'893850100023',N'Kitchen Pretend Play Set',N'Pretend play kitchen utensils and food accessories.',N'3+',230000,329000,N'Role Play',N'Local Toy Hub',110,N'/uploads/products/kitchen-play-set.jpg'),
    (N'893850100024',N'Doctor Pretend Play Kit',N'Toy medical kit for role-play doctor games.',N'3+',180000,269000,N'Role Play',N'Local Toy Hub',130,N'/uploads/products/doctor-play-kit.jpg'),
    (N'893850100025',N'Police Costume Role Play',N'Police costume set with badge and accessories.',N'4+',260000,359000,N'Role Play',N'Local Toy Hub',85,N'/uploads/products/police-costume.jpg'),
    (N'893850100026',N'Frozen Story Book and Puzzle',N'Character storybook with matching puzzle pieces.',N'4+',120000,179000,N'Creative',N'Local Toy Hub',140,N'/uploads/products/frozen-book-puzzle.jpg'),
    (N'893850100027',N'Peppa Pig Family Figure Set',N'Popular cartoon family mini figure set.',N'3+',170000,249000,N'Role Play',N'Local Toy Hub',125,N'/uploads/products/peppa-family-figures.jpg'),
    (N'893850100028',N'Transformer Robot Basic Figure',N'Convertible robot action figure starter line.',N'6+',290000,419000,N'Role Play',N'Hasbro APAC',95,N'/uploads/products/transformer-basic.jpg'),
    (N'893850100029',N'Chess Set Beginner Magnetic',N'Magnetic chess board set for beginner strategy.',N'7+',130000,199000,N'Board Games',N'Local Toy Hub',160,N'/uploads/products/chess-magnetic.jpg'),
    (N'893850100030',N'Puzzle 500 Pieces Ocean Theme',N'500-piece jigsaw puzzle with ocean landscape image.',N'10+',110000,169000,N'Board Games',N'Local Toy Hub',150,N'/uploads/products/puzzle-ocean-500.jpg');

    DECLARE @sqlMergeProducts NVARCHAR(MAX) = N'
    MERGE dbo.Products AS T
    USING (
        SELECT
            s.Barcode,
            s.ProductName,
            s.ProductDesc,
            s.AgeRange,
            s.ImportPrice,
            s.RetailPrice,
            s.InitialQty,
            s.ImagePath,
            c.Id AS CategoryId,
            sp.Id AS SupplierId
        FROM #ProductSeed s
        LEFT JOIN dbo.Categories c ON c.Name = s.CategoryName
        LEFT JOIN dbo.Suppliers sp ON sp.Name = s.SupplierName
    ) AS S
    ON T.Barcode = S.Barcode
    WHEN MATCHED THEN
        UPDATE SET
            T.Name = S.ProductName,
            T.Description = S.ProductDesc,
            T.AgeRange = S.AgeRange,
            T.ImportPrice = S.ImportPrice,
            T.RetailPrice = S.RetailPrice,
            T.StockQuantity = S.InitialQty,
            T.ImagePath = S.ImagePath,
            T.IsActive = 1,
            T.CategoryId = S.CategoryId,
            T.SupplierId = S.SupplierId
    WHEN NOT MATCHED THEN
        INSERT (Barcode, Name, [Description], AgeRange, ImportPrice, RetailPrice, StockQuantity, ImagePath, IsActive, SupplierId, CategoryId)
        VALUES (S.Barcode, S.ProductName, S.ProductDesc, S.AgeRange, S.ImportPrice, S.RetailPrice, S.InitialQty, S.ImagePath, 1, S.SupplierId, S.CategoryId);';
    EXEC sp_executesql @sqlMergeProducts;

    UPDATE p
    SET p.IsActive = 0
    FROM dbo.Products p
    WHERE p.Barcode NOT IN (SELECT Barcode FROM #ProductSeed);

    /* ------------------------------------------------------------
       5) Employee list (15 real names + full details)
    ------------------------------------------------------------ */
    CREATE TABLE #EmployeeSeed(
        FullName NVARCHAR(150),
        Gender NVARCHAR(10),
        BirthDate DATE,
        Email NVARCHAR(150),
        Address NVARCHAR(255),
        Phone NVARCHAR(20),
        PositionTitle NVARCHAR(120),
        ImagePath NVARCHAR(260)
    );

    INSERT INTO #EmployeeSeed(FullName, Gender, BirthDate, Email, Address, Phone, PositionTitle, ImagePath)
    VALUES
    (N'Nguyen Quang Minh',N'Male','1992-04-14',N'minh.nguyen@toyshop.vn',N'12 Le Van Sy, District 3, HCMC',N'0901000001',N'Store Manager',N'/uploads/staff/nguyen-quang-minh.jpg'),
    (N'Tran Thi Mai',N'Female','1995-09-21',N'mai.tran@toyshop.vn',N'25 Nguyen Trai, District 1, HCMC',N'0901000002',N'Assistant Manager',N'/uploads/staff/tran-thi-mai.jpg'),
    (N'Le Hoang Nam',N'Male','1998-01-11',N'nam.le@toyshop.vn',N'88 Kha Van Can, Thu Duc, HCMC',N'0901000003',N'Cashier',N'/uploads/staff/le-hoang-nam.jpg'),
    (N'Pham Thu Hang',N'Female','1997-12-02',N'hang.pham@toyshop.vn',N'60 Nguyen Huu Canh, Binh Thanh, HCMC',N'0901000004',N'Cashier',N'/uploads/staff/pham-thu-hang.jpg'),
    (N'Vo Gia Bao',N'Male','1994-07-10',N'bao.vo@toyshop.vn',N'14 Vo Van Tan, District 3, HCMC',N'0901000005',N'Sales Associate',N'/uploads/staff/vo-gia-bao.jpg'),
    (N'Bui Lan Anh',N'Female','1999-02-28',N'lananh.bui@toyshop.vn',N'41 Dien Bien Phu, District 3, HCMC',N'0901000006',N'Sales Associate',N'/uploads/staff/bui-lan-anh.jpg'),
    (N'Dang Tuan Kiet',N'Male','1996-05-06',N'kiet.dang@toyshop.vn',N'72 Tran Hung Dao, District 5, HCMC',N'0901000007',N'Inventory Clerk',N'/uploads/staff/dang-tuan-kiet.jpg'),
    (N'Do Thi Ngoc',N'Female','1998-10-03',N'ngoc.do@toyshop.vn',N'9 Hoang Hoa Tham, Tan Binh, HCMC',N'0901000008',N'Inventory Clerk',N'/uploads/staff/do-thi-ngoc.jpg'),
    (N'Nguyen Huu Phuc',N'Male','1991-11-19',N'phuc.nguyen@toyshop.vn',N'101 Huynh Tan Phat, District 7, HCMC',N'0901000009',N'Warehouse Lead',N'/uploads/staff/nguyen-huu-phuc.jpg'),
    (N'Trinh Thu Trang',N'Female','1993-08-08',N'trang.trinh@toyshop.vn',N'31 Le Van Viet, Thu Duc, HCMC',N'0901000010',N'Warehouse Staff',N'/uploads/staff/trinh-thu-trang.jpg'),
    (N'Pham Duc Anh',N'Male','1990-03-30',N'ducanh.pham@toyshop.vn',N'66 Nguyen Xi, Binh Thanh, HCMC',N'0901000011',N'Procurement Officer',N'/uploads/staff/pham-duc-anh.jpg'),
    (N'Hoang Yen Nhi',N'Female','1996-06-15',N'nhi.hoang@toyshop.vn',N'77 Luong Dinh Cua, Thu Duc, HCMC',N'0901000012',N'Customer Service',N'/uploads/staff/hoang-yen-nhi.jpg'),
    (N'Le Tuan Dat',N'Male','1997-04-04',N'dat.le@toyshop.vn',N'28 Cong Hoa, Tan Binh, HCMC',N'0901000013',N'Customer Service',N'/uploads/staff/le-tuan-dat.jpg'),
    (N'Ngo Thi Bich',N'Female','1995-12-25',N'bich.ngo@toyshop.vn',N'53 Bach Dang, Tan Binh, HCMC',N'0901000014',N'Visual Merchandiser',N'/uploads/staff/ngo-thi-bich.jpg'),
    (N'Phan Minh Tri',N'Male','1992-09-09',N'tri.phan@toyshop.vn',N'22 Quang Trung, Go Vap, HCMC',N'0901000015',N'Operations Analyst',N'/uploads/staff/phan-minh-tri.jpg');

    DECLARE @sqlMergeEmployees NVARCHAR(MAX) = N'
    MERGE dbo.Employees AS T
    USING #EmployeeSeed AS S
    ON T.Phone = S.Phone
    WHEN MATCHED THEN
        UPDATE SET
            T.FullName = S.FullName,
            T.Gender = S.Gender,
            T.BirthDate = S.BirthDate,
            T.Email = S.Email,
            T.Address = S.Address,
            T.PositionTitle = S.PositionTitle,
            T.ImagePath = S.ImagePath
    WHEN NOT MATCHED THEN
        INSERT (FullName, Gender, BirthDate, Email, Address, Phone, PositionTitle, ImagePath)
        VALUES (S.FullName, S.Gender, S.BirthDate, S.Email, S.Address, S.Phone, S.PositionTitle, S.ImagePath);';
    EXEC sp_executesql @sqlMergeEmployees;

    /* Keep Users linked to new employee set and sync display names used in invoices */
    IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND COL_LENGTH('dbo.Users', 'EmployeeID') IS NOT NULL
    BEGIN
        CREATE TABLE #SeedEmployeeMap(
            RN INT PRIMARY KEY,
            EmployeeID INT NOT NULL
        );

        INSERT INTO #SeedEmployeeMap(RN, EmployeeID)
        SELECT
            ROW_NUMBER() OVER (ORDER BY e.EmployeeID) AS RN,
            e.EmployeeID
        FROM dbo.Employees e
        INNER JOIN #EmployeeSeed s ON s.Phone = e.Phone;

        DECLARE @SeedEmpCount INT = (SELECT COUNT(*) FROM #SeedEmployeeMap);

        IF @SeedEmpCount > 0
        BEGIN
            ;WITH U AS (
                SELECT
                    u.UserID,
                    u.EmployeeID,
                    ROW_NUMBER() OVER (ORDER BY u.UserID) AS RN
                FROM dbo.Users u
            )
            UPDATE u
            SET u.EmployeeID = m.EmployeeID
            FROM dbo.Users u
            INNER JOIN U ux ON ux.UserID = u.UserID
            INNER JOIN #SeedEmployeeMap m ON m.RN = ((ux.RN - 1) % @SeedEmpCount) + 1;
        END;

        IF COL_LENGTH('dbo.Users', 'FullName') IS NOT NULL
        BEGIN
            UPDATE u
            SET u.FullName = e.FullName
            FROM dbo.Users u
            INNER JOIN dbo.Employees e ON e.EmployeeID = u.EmployeeID;
        END;
    END;

    /* Remove legacy sample employees not in new seed list */
    IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL AND COL_LENGTH('dbo.Users', 'EmployeeID') IS NOT NULL
    BEGIN
        DELETE e
        FROM dbo.Employees e
        LEFT JOIN #EmployeeSeed s ON s.Phone = e.Phone
        WHERE s.Phone IS NULL
          AND NOT EXISTS (
              SELECT 1
              FROM dbo.Users u
              WHERE u.EmployeeID = e.EmployeeID
          );
    END;
    ELSE
    BEGIN
        DELETE e
        FROM dbo.Employees e
        LEFT JOIN #EmployeeSeed s ON s.Phone = e.Phone
        WHERE s.Phone IS NULL;
    END;

    /* ------------------------------------------------------------
       6) Customer list (30 with complete details)
    ------------------------------------------------------------ */
    INSERT INTO dbo.Customers(FullName, PhoneNumber, Address, Points, IsMember)
    VALUES
    (N'Nguyen Thanh Binh',N'0910000001',N'12 Nguyen Dinh Chieu, District 1, HCMC',0,1),
    (N'Tran Kim Ngan',N'0910000002',N'44 Tran Nao, Thu Duc, HCMC',0,1),
    (N'Le Bao Chau',N'0910000003',N'19 Le Duc Tho, Go Vap, HCMC',0,1),
    (N'Pham Gia Huy',N'0910000004',N'8 Quoc Huong, Thu Duc, HCMC',0,1),
    (N'Vo Khanh Linh',N'0910000005',N'5 Nguyen Oanh, Go Vap, HCMC',0,1),
    (N'Dang Minh Quan',N'0910000006',N'67 Cong Hoa, Tan Binh, HCMC',0,1),
    (N'Bui Thanh Tam',N'0910000007',N'99 Le Van Luong, District 7, HCMC',0,1),
    (N'Hoang Y Nhi',N'0910000008',N'43 Phan Xich Long, Phu Nhuan, HCMC',0,1),
    (N'Nguyen Duc Long',N'0910000009',N'11 Pham Van Dong, Thu Duc, HCMC',0,1),
    (N'Trinh Quoc Viet',N'0910000010',N'120 Nguyen Van Cu, District 5, HCMC',0,1),
    (N'Le Minh Thu',N'0910000011',N'21 Hoang Sa, District 3, HCMC',0,1),
    (N'Pham Bao Nhi',N'0910000012',N'54 Truong Son, Tan Binh, HCMC',0,1),
    (N'Nguyen Hai Dang',N'0910000013',N'71 Ly Thuong Kiet, Tan Binh, HCMC',0,1),
    (N'Vu Phuong Anh',N'0910000014',N'3 D2 Street, Binh Thanh, HCMC',0,1),
    (N'Ta Gia Bao',N'0910000015',N'88 Nguyen Thai Hoc, District 1, HCMC',0,1),
    (N'Nguyen Hong Phuc',N'0910000016',N'16 Nguyen Thi Minh Khai, District 1, HCMC',0,1),
    (N'Pham Quynh Anh',N'0910000017',N'92 Le Loi, District 1, HCMC',0,1),
    (N'Le Van Tien',N'0910000018',N'15 Huynh Van Banh, Phu Nhuan, HCMC',0,1),
    (N'Doan Thuy Tien',N'0910000019',N'39 Hoang Van Thu, Phu Nhuan, HCMC',0,1),
    (N'Tran Anh Khoa',N'0910000020',N'110 Nguyen Gia Tri, Binh Thanh, HCMC',0,1),
    (N'Nguyen Thanh Truc',N'0910000021',N'9 To Hien Thanh, District 10, HCMC',0,1),
    (N'Bui Hoang Anh',N'0910000022',N'28 Su Van Hanh, District 10, HCMC',0,1),
    (N'Le Gia Han',N'0910000023',N'45 Cong Quynh, District 1, HCMC',0,1),
    (N'Phan Duc Khang',N'0910000024',N'100 Ba Hat, District 10, HCMC',0,1),
    (N'Nguyen Thu Hien',N'0910000025',N'77 Nguyen Van Linh, District 7, HCMC',0,1),
    (N'Tran Bao An',N'0910000026',N'32 Nguyen Son, Tan Phu, HCMC',0,1),
    (N'Pham Tuan Anh',N'0910000027',N'62 Lac Long Quan, Tan Binh, HCMC',0,1),
    (N'Dinh Khanh Vy',N'0910000028',N'23 Le Quang Dinh, Binh Thanh, HCMC',0,1),
    (N'Nguyen Gia Linh',N'0910000029',N'35 Nam Ky Khoi Nghia, District 3, HCMC',0,1),
    (N'Hoang Minh Khang',N'0910000030',N'49 Tran Quang Khai, District 1, HCMC',0,1);

    /* ------------------------------------------------------------
       7) March order data (10 orders/day, all month)
    ------------------------------------------------------------ */
    DECLARE @Users TABLE (RN INT IDENTITY(1,1), UserID INT);
    IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL
        INSERT INTO @Users(UserID) SELECT UserID FROM dbo.Users ORDER BY UserID;

    DECLARE @UserCount INT = (SELECT COUNT(*) FROM @Users);
    DECLARE @FallbackUserId INT = ISNULL((SELECT TOP 1 UserID FROM @Users), 1);

    DECLARE @ProductMap TABLE(
        RN INT IDENTITY(1,1),
        ProductID INT,
        RetailPrice DECIMAL(18,2),
        ImportPrice DECIMAL(18,2),
        InitialQty INT
    );

    INSERT INTO @ProductMap(ProductID, RetailPrice, ImportPrice, InitialQty)
    SELECT p.ProductID, p.RetailPrice, p.ImportPrice, s.InitialQty
    FROM dbo.Products p
    JOIN #ProductSeed s ON s.Barcode = p.Barcode
    WHERE p.IsActive = 1
    ORDER BY p.ProductID;

    DECLARE @d DATE = '2026-03-01';
    DECLARE @globalNo INT = 0;

    WHILE @d <= '2026-03-31'
    BEGIN
        DECLARE @n INT = 1;
        WHILE @n <= 10
        BEGIN
            SET @globalNo += 1;

            DECLARE @cid INT = ((@globalNo - 1) % 30) + 1;
            DECLARE @uid INT = CASE WHEN @UserCount > 0 THEN
                (SELECT UserID FROM @Users WHERE RN = ((@globalNo - 1) % @UserCount) + 1)
                ELSE @FallbackUserId END;

            DECLARE @pay NVARCHAR(20) = CASE WHEN (@globalNo % 3) = 0 THEN N'VNPay' ELSE N'Cash' END;
            DECLARE @orderDate DATETIME = DATEADD(MINUTE, ((@globalNo * 13) % 600), DATEADD(HOUR, 9, CAST(@d AS DATETIME)));

            DECLARE @oid INT;
            DECLARE @sqlInsertOrder NVARCHAR(MAX) = N'
                INSERT INTO dbo.Orders(OrderDate, UserID, CustomerID, TotalAmount, Status, PaymentMethod)
                VALUES (@OrderDate, @UserID, @CustomerID, 0, N''Paid'', @PaymentMethod);
                SELECT @NewOrderId = CAST(SCOPE_IDENTITY() AS INT);';
            EXEC sp_executesql
                @sqlInsertOrder,
                N'@OrderDate DATETIME, @UserID INT, @CustomerID INT, @PaymentMethod NVARCHAR(20), @NewOrderId INT OUTPUT',
                @OrderDate = @orderDate,
                @UserID = @uid,
                @CustomerID = @cid,
                @PaymentMethod = @pay,
                @NewOrderId = @oid OUTPUT;
            DECLARE @k INT = 1;
            WHILE @k <= 3
            BEGIN
                DECLARE @prn INT = ((@globalNo + (@k * 7)) % 30) + 1;
                DECLARE @pid INT = (SELECT ProductID FROM @ProductMap WHERE RN = @prn);
                DECLARE @price DECIMAL(18,2) = (SELECT RetailPrice FROM @ProductMap WHERE RN = @prn);
                DECLARE @qty INT = CASE WHEN ((@globalNo + @k) % 5) = 0 THEN 3 WHEN ((@globalNo + @k) % 2) = 0 THEN 2 ELSE 1 END;

                INSERT INTO dbo.OrderDetails(OrderID, ProductID, Quantity, UnitPrice)
                VALUES (@oid, @pid, @qty, @price);

                SET @k += 1;
            END;

            UPDATE o
            SET o.TotalAmount = x.TotalAmount
            FROM dbo.Orders o
            CROSS APPLY (
                SELECT SUM(od.Quantity * od.UnitPrice) AS TotalAmount
                FROM dbo.OrderDetails od
                WHERE od.OrderID = o.OrderID
            ) x
            WHERE o.OrderID = @oid;

            SET @n += 1;
        END;

        SET @d = DATEADD(DAY, 1, @d);
    END;

    /* ------------------------------------------------------------
       8) Update stock after sold quantities + points by spending
    ------------------------------------------------------------ */
    ;WITH Sold AS (
        SELECT od.ProductID, SUM(od.Quantity) AS SoldQty
        FROM dbo.OrderDetails od
        GROUP BY od.ProductID
    )
    UPDATE p
    SET p.StockQuantity =
        CASE
            WHEN (pm.InitialQty - ISNULL(s.SoldQty, 0)) < 0 THEN 0
            ELSE (pm.InitialQty - ISNULL(s.SoldQty, 0))
        END
    FROM dbo.Products p
    JOIN @ProductMap pm ON pm.ProductID = p.ProductID
    LEFT JOIN Sold s ON s.ProductID = p.ProductID;

    ;WITH Spend AS (
        SELECT CustomerID, SUM(TotalAmount) AS TotalSpend
        FROM dbo.Orders
        GROUP BY CustomerID
    )
    UPDATE c
    SET
        c.Points = CAST(ROUND(ISNULL(s.TotalSpend, 0) * 0.10, 0) AS INT),
        c.IsMember = 1
    FROM dbo.Customers c
    LEFT JOIN Spend s ON s.CustomerID = c.CustomerID;

    /* ------------------------------------------------------------
       9) Inventory import records + return records
    ------------------------------------------------------------ */
    DECLARE @batch INT = 1;
    WHILE @batch <= 10
    BEGIN
        DECLARE @inHeaderId INT;
        DECLARE @inNote NVARCHAR(255) = N'PO batch import #' + CAST(@batch AS NVARCHAR(20));
        DECLARE @inCreatedAt DATETIME = DATEADD(DAY, (@batch * 2) - 2, CAST('2026-03-01' AS DATETIME));
        DECLARE @inReceipt NVARCHAR(260) = N'/uploads/invoices/po-202603-' + RIGHT('00' + CAST(@batch AS NVARCHAR(2)), 2) + N'.pdf';
        DECLARE @sqlInsertInHeader NVARCHAR(MAX) = N'
            INSERT INTO dbo.StockMovementHeaders
                (MovementType, AffectsStock, Note, CreatedByUserID, CreatedAt, ReceiptAttachment, PreparedBy, ApprovedBy)
            VALUES
                (N''IN'', 0, @Note, @CreatedByUserID, @CreatedAt, @ReceiptAttachment, @PreparedBy, @ApprovedBy);
            SELECT @NewMovementId = CAST(SCOPE_IDENTITY() AS INT);';
        EXEC sp_executesql
            @sqlInsertInHeader,
            N'@Note NVARCHAR(255), @CreatedByUserID INT, @CreatedAt DATETIME, @ReceiptAttachment NVARCHAR(260), @PreparedBy NVARCHAR(120), @ApprovedBy NVARCHAR(120), @NewMovementId INT OUTPUT',
            @Note = @inNote,
            @CreatedByUserID = @FallbackUserId,
            @CreatedAt = @inCreatedAt,
            @ReceiptAttachment = @inReceipt,
            @PreparedBy = N'Procurement Team',
            @ApprovedBy = N'Store Manager',
            @NewMovementId = @inHeaderId OUTPUT;

        DECLARE @line INT = 1;
        WHILE @line <= 3
        BEGIN
            DECLARE @prn2 INT = ((@batch - 1) * 3 + @line);
            IF @prn2 > 30 SET @prn2 = @prn2 - 30;

            INSERT INTO dbo.StockMovementLines(MovementID, ProductID, Quantity, UnitPrice)
            SELECT
                @inHeaderId,
                ProductID,
                (12 + ((@batch + @line) % 9)),
                ImportPrice
            FROM @ProductMap
            WHERE RN = @prn2;

            SET @line += 1;
        END;

        SET @batch += 1;
    END;

    DECLARE @returnBatch INT = 1;
    WHILE @returnBatch <= 8
    BEGIN
        DECLARE @retHeaderId INT;
        DECLARE @retMethod NVARCHAR(20) = CASE WHEN (@returnBatch % 2) = 0 THEN N'VNPay' ELSE N'Cash' END;

        DECLARE @retNote NVARCHAR(255) = N'Defective product return #' + CAST(@returnBatch AS NVARCHAR(20));
        DECLARE @retCreatedAt DATETIME = DATEADD(DAY, (@returnBatch * 3), CAST('2026-03-01' AS DATETIME));
        DECLARE @retEvidence NVARCHAR(260) = N'/uploads/returns/defect-202603-' + RIGHT('00' + CAST(@returnBatch AS NVARCHAR(2)), 2) + N'.jpg';
        DECLARE @retRefundAmount DECIMAL(18,2) = (450000 + (@returnBatch * 55000));
        DECLARE @sqlInsertOutHeader NVARCHAR(MAX) = N'
            INSERT INTO dbo.StockMovementHeaders
                (MovementType, AffectsStock, Note, CreatedByUserID, CreatedAt, Reason, EvidenceImagePath, PreparedBy, ApprovedBy, RefundAmount, RefundMethod)
            VALUES
                (N''OUT'', 0, @Note, @CreatedByUserID, @CreatedAt, @Reason, @EvidenceImagePath, @PreparedBy, @ApprovedBy, @RefundAmount, @RefundMethod);
            SELECT @NewMovementId = CAST(SCOPE_IDENTITY() AS INT);';
        EXEC sp_executesql
            @sqlInsertOutHeader,
            N'@Note NVARCHAR(255), @CreatedByUserID INT, @CreatedAt DATETIME, @Reason NVARCHAR(255), @EvidenceImagePath NVARCHAR(260), @PreparedBy NVARCHAR(120), @ApprovedBy NVARCHAR(120), @RefundAmount DECIMAL(18,2), @RefundMethod NVARCHAR(20), @NewMovementId INT OUTPUT',
            @Note = @retNote,
            @CreatedByUserID = @FallbackUserId,
            @CreatedAt = @retCreatedAt,
            @Reason = N'Defect found during quality check',
            @EvidenceImagePath = @retEvidence,
            @PreparedBy = N'Inventory Clerk',
            @ApprovedBy = N'Store Manager',
            @RefundAmount = @retRefundAmount,
            @RefundMethod = @retMethod,
            @NewMovementId = @retHeaderId OUTPUT;

        INSERT INTO dbo.StockMovementLines(MovementID, ProductID, Quantity, UnitPrice)
        SELECT
            @retHeaderId,
            ProductID,
            (1 + (@returnBatch % 3)),
            ImportPrice
        FROM @ProductMap
        WHERE RN = ((@returnBatch * 4) % 30) + 1;

        SET @returnBatch += 1;
    END;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
END CATCH;

