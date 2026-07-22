-- =============================================================================
-- 修正 Orders.total 語意：回歸「純商品小計」（= Σ Orderdetails.subtotal）
-- Run on: tfoodies (SQL Server)
-- 冪等、可安全重跑。
--
-- 背景：舊系統（前後台一致）Orders.total = 純商品小計（= Σ Orderdetails.subtotal，
--   不含運費、不含訂單層折扣 Orders.discount），所有消費端一律 應付 = total + freight - discount。
--   * 逐項折數走 Orderdetails.discount（折數），已併入 Orderdetails.subtotal；
--   * 訂單層折扣走 Orders.discount（元），獨立、不併入 total。
--   新系統一度把「最終金額(subtotal+freight-discount)」寫進 total，導致發票/FISC/入帳/會計
--   把運費與折扣重複計入一次（B2B 折扣單最明顯：折扣被扣兩次）。
--
-- 安全設計：只更正「明確符合新系統雙重扣特徵」的列，即
--     stored total == (Σ subtotal) + ISNULL(freight,0) - ISNULL(discount,0)  且  != Σ subtotal
--   舊系統訂單 total 本就 = Σ subtotal，一律不符此特徵 → 絕不會被動到。
--   任何「有差異但不符特徵」的列只列出供人工複核，不自動修改。
--   無明細的訂單(Σ 為 NULL)一律略過。
-- =============================================================================

SET NOCOUNT ON;
-- Orders 上有 filtered index，UPDATE 需這兩個 SET 為 ON（否則 Msg 1934）。
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

;WITH od AS (
    SELECT orderid, SUM(subtotal) AS lineSubtotal
    FROM Orderdetails
    GROUP BY orderid
)
SELECT
    -- 將被更正（符合新系統雙重扣特徵）
    SUM(CASE WHEN o.total = od.lineSubtotal + ISNULL(o.freight,0) - ISNULL(o.discount,0)
                  AND o.total <> od.lineSubtotal
             THEN 1 ELSE 0 END)                                  AS will_fix,
    -- 需人工複核（有差異但不符特徵）
    SUM(CASE WHEN o.total <> od.lineSubtotal
                  AND NOT (o.total = od.lineSubtotal + ISNULL(o.freight,0) - ISNULL(o.discount,0))
             THEN 1 ELSE 0 END)                                  AS needs_review,
    -- 已正確（total = Σ subtotal）
    SUM(CASE WHEN o.total = od.lineSubtotal THEN 1 ELSE 0 END)   AS already_ok
FROM Orders o
JOIN od ON od.orderid = o.orderid
WHERE od.lineSubtotal IS NOT NULL;

-- 逐筆列出「需人工複核」的異常列（有差異但不符雙重扣特徵）— 不會被本腳本更改
;WITH od AS (
    SELECT orderid, SUM(subtotal) AS lineSubtotal
    FROM Orderdetails GROUP BY orderid
)
SELECT o.ordercode, o.total AS stored_total, od.lineSubtotal AS sum_subtotal,
       ISNULL(o.freight,0) AS freight, ISNULL(o.discount,0) AS discount, o.createdate
FROM Orders o
JOIN od ON od.orderid = o.orderid
WHERE od.lineSubtotal IS NOT NULL
  AND o.total <> od.lineSubtotal
  AND NOT (o.total = od.lineSubtotal + ISNULL(o.freight,0) - ISNULL(o.discount,0))
ORDER BY o.createdate DESC;

-- 執行更正：僅限符合新系統雙重扣特徵的列，total ← Σ Orderdetails.subtotal
;WITH od AS (
    SELECT orderid, SUM(subtotal) AS lineSubtotal
    FROM Orderdetails GROUP BY orderid
)
UPDATE o
SET o.total = od.lineSubtotal
FROM Orders o
JOIN od ON od.orderid = o.orderid
WHERE od.lineSubtotal IS NOT NULL
  AND o.total = od.lineSubtotal + ISNULL(o.freight,0) - ISNULL(o.discount,0)
  AND o.total <> od.lineSubtotal;

PRINT '完成：符合新系統雙重扣特徵的 Orders.total 已校正為 Σ Orderdetails.subtotal（純商品小計）。';
PRINT '請檢視上方 needs_review 清單（若有）並人工處理，本腳本未更動這些列。';
GO
