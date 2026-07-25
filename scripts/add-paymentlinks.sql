-- =============================================================================
-- TFoodies — Paymentlinks 刷卡收款連結（後台產生一次性刷卡連結給客人付款）
-- Run on: tfoodies (SQL Server)
--
-- 用途：不走商城結帳流程的臨時收款（客訂、補款、活動費用）。後台填金額產生連結，
-- 客人開連結填收件資料後直接刷卡（FISC FOCAS_WEBPOS），成功後通知營運人工入帳/開票。
--
-- 刻意不寫入 Orders / Incomes：
--   Orders.memberid 與 Incomes.memberid 皆為 NOT NULL + FK 到 Members，而付款連結
--   不綁會員；硬塞假 memberid 會污染會員數與會計報表（銷貨收入有金額卻無對應
--   Orderdetails/Invoicedetails，帳會不平）。金額只存在本表，由營運人工入帳。
--
-- 本專案為 EF Core Database-First / scaffold，無 migration 機制；schema 變更以本類
-- 冪等 SQL 腳本手動執行。可安全重複執行。
-- =============================================================================

-- ── Paymentlinks：收款連結主檔 ───────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'Paymentlinks' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.Paymentlinks (
        paymentlinkid     uniqueidentifier NOT NULL,
        -- 收款單號，同時作為 FISC WEBPOS 的 lidm（格式 PL+yyyyMMdd+3 碼＝13 字元，
        -- 遠低於手冊 v2.7 的 AN 19 上限）。由 Paymentlinkcodes 原子產生。
        paymentlinkcode   nvarchar(20)     NOT NULL,
        -- 客人連結網址用的不可猜測 token（32 字元小寫 hex ＝ 128-bit CSPRNG）。
        token             nvarchar(64)     NOT NULL,
        title             nvarchar(100)    NOT NULL,  -- 收款項目（顯示給客人）
        note              nvarchar(500)    NULL,      -- 內部備註（不回傳客人端）
        -- 最終實收金額。不加運費、不套折扣碼：管理員填多少就收多少。
        amount            int              NOT NULL,
        -- 0=未付款 1=已付款 2=已作廢
        status            int              NOT NULL CONSTRAINT DF_Paymentlinks_Status DEFAULT (0),
        customername      nvarchar(50)     NULL,      -- 以下四欄由客人於付款頁填寫
        customermobile    nvarchar(20)     NULL,
        customerzipcodeid int              NULL,      -- 縣市/鄉鎮市區
        customeraddress   nvarchar(200)    NULL,      -- 詳細地址（不含縣市/區）
        lastpan4          nvarchar(4)      NULL,      -- 卡號末四碼（對齊 Orders.lastpan4）
        txnref            nvarchar(200)    NULL,      -- FISC authCode:xxx xid:yyy
        paydate           datetime         NULL,
        expiredate        datetime         NULL,      -- NULL = 不限期
        -- 建立者 AdminID。刻意不加 FK 到 Admins：itadmin 後門帳號 (AdminID 888) 在
        -- Admins 表沒有資料列（見 SqlAdminPermissionService），加 FK 會讓它建連結時 547 失敗。
        createadminid     int              NOT NULL,
        createdate        datetime         NOT NULL,
        updatedate        datetime         NULL,
        CONSTRAINT PK_Paymentlinks PRIMARY KEY CLUSTERED (paymentlinkid),
        CONSTRAINT FK_Paymentlinks_Zipcodes
            FOREIGN KEY (customerzipcodeid) REFERENCES dbo.Zipcodes(zipcodeid)
    );
END;
GO

-- token 唯一：碰撞的最後防線（應用層以 CSPRNG 產生，實務上不會撞）
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Paymentlinks_Token'
               AND object_id = OBJECT_ID('dbo.Paymentlinks'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Paymentlinks_Token
        ON dbo.Paymentlinks (token);
END;
GO

-- 單號唯一：防重複 lidm 送進 FISC
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Paymentlinks_Code'
               AND object_id = OBJECT_ID('dbo.Paymentlinks'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Paymentlinks_Code
        ON dbo.Paymentlinks (paymentlinkcode);
END;
GO

-- 後台列表：狀態篩選 + 建立時間新到舊
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Paymentlinks_Status_Createdate'
               AND object_id = OBJECT_ID('dbo.Paymentlinks'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Paymentlinks_Status_Createdate
        ON dbo.Paymentlinks (status, createdate DESC)
        INCLUDE (paymentlinkcode, title, amount, customername);
END;
GO

-- ── Paymentlinkcodes：日期分組流水號（結構比照既有 Ordercodes）─────────────────
-- 供 ICodeNumberService / SqlCodeNumberService 的 MERGE WITH (HOLDLOCK) 使用。
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'Paymentlinkcodes' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.Paymentlinkcodes (
        paymentlinkcodeid uniqueidentifier NOT NULL,
        year              nvarchar(4)      NOT NULL,
        month             nvarchar(2)      NOT NULL,
        day               nvarchar(2)      NOT NULL,
        code              int              NOT NULL,
        CONSTRAINT PK_Paymentlinkcodes PRIMARY KEY CLUSTERED (paymentlinkcodeid)
    );
END;
GO

-- MERGE 的比對鍵，需唯一索引避免同日多列
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Paymentlinkcodes_Date'
               AND object_id = OBJECT_ID('dbo.Paymentlinkcodes'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Paymentlinkcodes_Date
        ON dbo.Paymentlinkcodes (year, month, day);
END;
GO
