-- =============================================================================
-- TFoodies — Paymentattempts 刷卡授權結果紀錄
-- Run on: tfoodies (SQL Server)
--
-- 用途：把財金 FISC FOCAS_WEBPOS 每一次授權回呼的結果（成功與失敗）留存下來。
--
-- 為什麼需要這張表：
--   顧客常回報「刷卡沒有成功」，但系統原本只讀 status/authCode 判定成敗，
--   把財金回傳的 errcode（錯誤代碼）與 errDesc（中文失敗原因說明）直接丟棄，
--   失敗後也不記 log、不寫任何資料，導致客服與工程都無法回答「為什麼失敗」。
--   有了本表，後台訂單詳情可直接顯示刷卡紀錄，並可統計失敗原因分布：
--     SELECT errcode, errdesc, COUNT(*) FROM Paymentattempts
--     WHERE issuccess = 0 GROUP BY errcode, errdesc ORDER BY COUNT(*) DESC;
--
-- ⚠️ 不儲存卡號：財金雖回傳遮罩卡號 pan（如 480254******9104），本表刻意只留
--    lastPan4 與 cardBrand，避免任何形式的卡號落地。
--
-- 刻意不加 FK 到 Orders：收款連結（Paymentlinks）的 lidm 為 PL 開頭、不在 Orders，
-- 兩者共用同一組回呼解析與本表。
--
-- 本專案為 EF Core Database-First / scaffold，無 migration 機制；schema 變更以本類
-- 冪等 SQL 腳本手動執行。可安全重複執行。
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'Paymentattempts' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.Paymentattempts (
        paymentattemptid uniqueidentifier NOT NULL,
        -- 財金 lidm：訂單編號（O+8 碼日期+3 碼）或收款單號（PL 開頭）
        lidm             nvarchar(20)     NOT NULL,
        -- 回呼來源：return（前台刷卡）/ return-admin（後台代客刷卡）/
        --           notify（財金主動通知）/ return-paylink（收款連結）
        source           nvarchar(20)     NOT NULL,
        -- 授權是否成功（status='0' 且 authCode 非空）
        issuccess        bit              NOT NULL,
        status           nvarchar(2)      NULL,   -- 財金授權結果狀態（手冊 §3.1.2）
        errcode          nvarchar(4)      NULL,   -- 財金錯誤代碼（手冊 §5 錯誤代碼一覽表）
        errdesc          nvarchar(512)    NULL,   -- 財金授權失敗原因說明（中文）
        authcode         nvarchar(10)     NULL,   -- 交易授權碼（成功才有）
        xid              nvarchar(64)     NULL,   -- 交易追蹤碼
        lastpan4         nvarchar(4)      NULL,   -- 卡號末四碼（對齊 Orders.lastpan4）
        cardbrand        nvarchar(20)     NULL,   -- VISA / MasterCard / JCB
        authamt          int              NULL,   -- 授權金額（台幣整數）
        -- 我方補充說明，例如「授權成功但入帳處理拋出例外，已交由主動通知補償」
        note             nvarchar(500)    NULL,
        createdate       datetime         NOT NULL,
        CONSTRAINT PK_Paymentattempts PRIMARY KEY CLUSTERED (paymentattemptid)
    );
END;
GO

-- 後台訂單詳情「刷卡紀錄」：依單號查、新到舊
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Paymentattempts_Lidm_Createdate'
               AND object_id = OBJECT_ID('dbo.Paymentattempts'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Paymentattempts_Lidm_Createdate
        ON dbo.Paymentattempts (lidm, createdate DESC);
END;
GO

-- 失敗原因統計：WHERE issuccess = 0 GROUP BY errcode
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Paymentattempts_Issuccess_Createdate'
               AND object_id = OBJECT_ID('dbo.Paymentattempts'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Paymentattempts_Issuccess_Createdate
        ON dbo.Paymentattempts (issuccess, createdate DESC)
        INCLUDE (lidm, errcode, errdesc);
END;
GO
