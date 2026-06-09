# Scaffolded/ — GENERATED, do not hand-edit

`TfoodiesContext` and `Entities/*.cs` here are produced by EF Core Database-First scaffolding
against the frozen `tfoodies` schema. Generate / refresh them with:

```bash
TFOODIES_CONNSTRING='...' ./scripts/scaffold-db.sh
```

Rules:
- **Never edit by hand.** Re-running the scaffold and getting an empty diff is our schema-drift guard.
- Ugly DB names stay verbatim here (`Refound`, `quantity_left`, `Orderdetailstock`, …).
  Clean domain names (`Refund`, `OrderLineStock`, …) are introduced ONLY in the `Mappers/` layer.
- Undeclared FKs (`Expendituredetails.purchasedetailid`, `Viewlogs.memberid`, `Stocks.purchaseid`)
  are added as relationships via partial `IEntityTypeConfiguration` in `../Configurations/`
  using `HasNoConstraint()` — EF can join them, but NO DDL is emitted.

Until the scaffold has been run (needs a DB connection), this folder is intentionally empty
except this README.
