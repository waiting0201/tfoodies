-- =============================================================================
-- TFoodies Performance Indexes
-- Run on: tfoodies (SQL Server)
-- Checks for existence before creating to allow safe re-runs.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Orders(paystatus, deliverstatus) — high-frequency status queries in admin/API
-- -----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Orders')
      AND name = 'IX_Orders_PayStatus_DeliverStatus'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Orders_PayStatus_DeliverStatus
        ON dbo.Orders (paystatus, deliverstatus)
        INCLUDE (orderid, ordercode, memberid, createdate);
END;
GO

-- -----------------------------------------------------------------------------
-- Orders(expirepaydate) WHERE paytype=2 AND paystatus=0 — ATM expiry daily job
-- -----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Orders')
      AND name = 'IX_Orders_ExpirePayDate_Atm'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Orders_ExpirePayDate_Atm
        ON dbo.Orders (expirepaydate)
        WHERE paytype = 2 AND paystatus = 0;
END;
GO

-- -----------------------------------------------------------------------------
-- Members(mobile) — member lookup by mobile (login / SMS verification)
-- -----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Members')
      AND name = 'IX_Members_Mobile'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Members_Mobile
        ON dbo.Members (mobile)
        INCLUDE (memberid, name, password, isenable);
END;
GO

-- -----------------------------------------------------------------------------
-- Products(isdisabled, ishot) — storefront listing queries
-- -----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Products')
      AND name = 'IX_Products_IsDisabled_IsHot'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Products_IsDisabled_IsHot
        ON dbo.Products (isdisabled, ishot)
        INCLUDE (productid, title, price, fixprice);
END;
GO

-- -----------------------------------------------------------------------------
-- Warehousestocks(stockid, quantity_left) WHERE quantity_left > 0
-- — FIFO stock allocation (WarehouseStocksService.GetStockWarehouses)
-- -----------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Warehousestocks')
      AND name = 'IX_Warehousestocks_Stock_QuantityLeft'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Warehousestocks_Stock_QuantityLeft
        ON dbo.Warehousestocks (stockid, quantity_left)
        WHERE quantity_left > 0;
END;
GO
