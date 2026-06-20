-- =============================================================================
-- TFoodies — Knowledgeproducts 連結表（小知識 ↔ 商品 多對多）
-- Run on: tfoodies (SQL Server)
-- 結構比照既有 Issueproducts：複合 PK (knowledgeid, productid) + 兩個 FK。
-- 本專案為 EF Core Database-First / scaffold，無 migration 機制；schema 變更以本類
-- 冪等 SQL 腳本手動執行。可安全重複執行。
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'Knowledgeproducts' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.Knowledgeproducts (
        knowledgeid uniqueidentifier NOT NULL,
        productid   uniqueidentifier NOT NULL,
        CONSTRAINT PK_Knowledgeproducts PRIMARY KEY CLUSTERED (knowledgeid, productid),
        CONSTRAINT FK_Knowledgeproducts_Knowledges
            FOREIGN KEY (knowledgeid) REFERENCES dbo.Knowledges(knowledgeid),
        CONSTRAINT FK_Knowledgeproducts_Products
            FOREIGN KEY (productid)  REFERENCES dbo.Products(productid)
    );
END;
GO
