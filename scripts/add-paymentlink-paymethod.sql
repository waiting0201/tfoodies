-- =============================================================================
-- TFoodies — Paymentlinks 追加「付款方式」欄位（收款連結支援 LINE Pay）
-- Run on: tfoodies (SQL Server)
--
-- 後台建立收款連結時指定客人要用哪種方式付款：
--   1 = 信用卡（FISC FOCAS_WEBPOS，既有行為）
--   8 = LINE Pay（LINE Pay Online API v3）
-- 值刻意對齊 Domain 的 PayType 列舉（src/TFoodies.Domain/Enums/Enums.cs），
-- 未來若再開放其他方式，直接沿用同一組編碼即可。
--
-- DEFAULT 1：既有資料列全部是信用卡連結，補欄位後行為不變。
--
-- 本專案為 EF Core Database-First / scaffold，無 migration 機制；schema 變更以本類
-- 冪等 SQL 腳本手動執行。可安全重複執行。
-- 前置：scripts/add-paymentlinks.sql（建 Paymentlinks / Paymentlinkcodes）
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Paymentlinks') AND name = 'paymethod'
)
BEGIN
    ALTER TABLE dbo.Paymentlinks
        ADD paymethod int NOT NULL
            CONSTRAINT DF_Paymentlinks_Paymethod DEFAULT (1);
END;
GO
