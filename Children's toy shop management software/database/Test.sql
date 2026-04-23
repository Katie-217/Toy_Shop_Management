WITH CTE AS (
    SELECT 
        ProductID,
        ROW_NUMBER() OVER (ORDER BY ProductID) AS rn
    FROM Products
)
UPDATE p
SET ImagePath = '/uploads/products/' + CAST(c.rn AS NVARCHAR) + '.jpg'
FROM Products p
JOIN CTE c ON p.ProductID = c.ProductID
WHERE c.rn <= 33;