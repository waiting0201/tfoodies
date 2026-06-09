using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using TFoodies.Infrastructure.Persistence.Scaffolded;

namespace TFoodies.Infrastructure.Persistence;

public partial class TfoodiesContext : DbContext
{
    public TfoodiesContext(DbContextOptions<TfoodiesContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Accounting> Accountings { get; set; }

    public virtual DbSet<Admin> Admins { get; set; }

    public virtual DbSet<AdminLim> AdminLims { get; set; }

    public virtual DbSet<Atmcode> Atmcodes { get; set; }

    public virtual DbSet<Banner> Banners { get; set; }

    public virtual DbSet<Blog> Blogs { get; set; }

    public virtual DbSet<Brand> Brands { get; set; }

    public virtual DbSet<Brandphoto> Brandphotos { get; set; }

    public virtual DbSet<Declaration> Declarations { get; set; }

    public virtual DbSet<Declarationdetail> Declarationdetails { get; set; }

    public virtual DbSet<Discount> Discounts { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<Eventphoto> Eventphotos { get; set; }

    public virtual DbSet<Exchange> Exchanges { get; set; }

    public virtual DbSet<Expenditure> Expenditures { get; set; }

    public virtual DbSet<Expenditurecode> Expenditurecodes { get; set; }

    public virtual DbSet<Expendituredetail> Expendituredetails { get; set; }

    public virtual DbSet<GlobalMyB2B> GlobalMyB2Bs { get; set; }

    public virtual DbSet<Income> Incomes { get; set; }

    public virtual DbSet<Incomecode> Incomecodes { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<Invoicecode> Invoicecodes { get; set; }

    public virtual DbSet<Invoicedetail> Invoicedetails { get; set; }

    public virtual DbSet<Issue> Issues { get; set; }

    public virtual DbSet<Issuemedia> Issuemedias { get; set; }

    public virtual DbSet<Knowledge> Knowledges { get; set; }

    public virtual DbSet<Knowledgemedia> Knowledgemedias { get; set; }

    public virtual DbSet<Lim> Lims { get; set; }

    public virtual DbSet<Logistic> Logistics { get; set; }

    public virtual DbSet<Member> Members { get; set; }

    public virtual DbSet<Newmedia> Newmedias { get; set; }

    public virtual DbSet<News> News { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<Ordercode> Ordercodes { get; set; }

    public virtual DbSet<Orderdetail> Orderdetails { get; set; }

    public virtual DbSet<Orderdetailstock> Orderdetailstocks { get; set; }

    public virtual DbSet<Outcome> Outcomes { get; set; }

    public virtual DbSet<Outcomecode> Outcomecodes { get; set; }

    public virtual DbSet<Outofnotice> Outofnotices { get; set; }

    public virtual DbSet<Preorder> Preorders { get; set; }

    public virtual DbSet<Preorderdetail> Preorderdetails { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Productmedia> Productmedias { get; set; }

    public virtual DbSet<Productphoto> Productphotos { get; set; }

    public virtual DbSet<Producttype> Producttypes { get; set; }

    public virtual DbSet<Promote> Promotes { get; set; }

    public virtual DbSet<Promoteproduct> Promoteproducts { get; set; }

    public virtual DbSet<Purchase> Purchases { get; set; }

    public virtual DbSet<Purchasecode> Purchasecodes { get; set; }

    public virtual DbSet<Purchasedetail> Purchasedetails { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<Questionmedia> Questionmedias { get; set; }

    public virtual DbSet<Questiontype> Questiontypes { get; set; }

    public virtual DbSet<Recipe> Recipes { get; set; }

    public virtual DbSet<Recipeingredient> Recipeingredients { get; set; }

    public virtual DbSet<Recipeseasoning> Recipeseasonings { get; set; }

    public virtual DbSet<Recipestep> Recipesteps { get; set; }

    public virtual DbSet<Refound> Refounds { get; set; }

    public virtual DbSet<Refoundcode> Refoundcodes { get; set; }

    public virtual DbSet<Return> Returns { get; set; }

    public virtual DbSet<Returncode> Returncodes { get; set; }

    public virtual DbSet<Returndetail> Returndetails { get; set; }

    public virtual DbSet<Setproduct> Setproducts { get; set; }

    public virtual DbSet<Sm> Sms { get; set; }

    public virtual DbSet<Smsdetail> Smsdetails { get; set; }

    public virtual DbSet<Stock> Stocks { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<Viewlog> Viewlogs { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    public virtual DbSet<Warehousestock> Warehousestocks { get; set; }

    public virtual DbSet<Zipcode> Zipcodes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Chinese_Taiwan_Stroke_CI_AS");

        modelBuilder.Entity<Accounting>(entity =>
        {
            entity.Property(e => e.accountingid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(e => e.AdminID).HasName("PK_Admins_1");

            entity.Property(e => e.Isenable).HasDefaultValue((byte)1);
        });

        modelBuilder.Entity<AdminLim>(entity =>
        {
            entity.Property(e => e.AdminLimID).ValueGeneratedNever();

            entity.HasOne(d => d.Admin).WithMany(p => p.AdminLims)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdminLims_Admins");

            entity.HasOne(d => d.Lim).WithMany(p => p.AdminLims)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdminLims_Lims");
        });

        modelBuilder.Entity<Atmcode>(entity =>
        {
            entity.Property(e => e.atmcodeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Banner>(entity =>
        {
            entity.Property(e => e.bannerid).ValueGeneratedNever();
            entity.Property(e => e.style).HasDefaultValue(1);
        });

        modelBuilder.Entity<Blog>(entity =>
        {
            entity.Property(e => e.blogid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.Property(e => e.brandid).ValueGeneratedNever();
            entity.Property(e => e.isdisplay).HasDefaultValue(1);

            entity.HasOne(d => d.supplier).WithMany(p => p.Brands).HasConstraintName("FK_Brands_Suppliers");
        });

        modelBuilder.Entity<Brandphoto>(entity =>
        {
            entity.Property(e => e.brandphotoid).ValueGeneratedNever();

            entity.HasOne(d => d.brand).WithMany(p => p.Brandphotos).HasConstraintName("FK_Brandphotos_Brands");
        });

        modelBuilder.Entity<Declaration>(entity =>
        {
            entity.Property(e => e.declarationid).ValueGeneratedNever();
            entity.Property(e => e.declarationtype)
                .HasDefaultValue(1)
                .HasComment("1:月; 2:日");
            entity.Property(e => e.soldtarget)
                .HasDefaultValue(1)
                .HasComment("1:下游業者; 2:消費者; 3: 自用");
        });

        modelBuilder.Entity<Declarationdetail>(entity =>
        {
            entity.Property(e => e.declarationdetailid).ValueGeneratedNever();

            entity.HasOne(d => d.declaration).WithMany(p => p.Declarationdetails).HasConstraintName("FK_Declarationdetails_Declarations");

            entity.HasOne(d => d.order).WithMany(p => p.Declarationdetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Declarationdetails_Orders");
        });

        modelBuilder.Entity<Discount>(entity =>
        {
            entity.Property(e => e.discountid).ValueGeneratedNever();
            entity.Property(e => e.isdisable).HasComment("0:否; 1:是");
            entity.Property(e => e.isonetime).HasComment("0:否; 1:是; 2:每會員限用一次");
            entity.Property(e => e.istype).HasComment("0:折扣; 1:金額");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.eventid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Eventphoto>(entity =>
        {
            entity.HasKey(e => e.eventphotoid).HasName("PK_Eventmedias");

            entity.Property(e => e.eventphotoid).ValueGeneratedNever();

            entity.HasOne(d => d._event).WithMany(p => p.Eventphotos).HasConstraintName("FK_Eventmedias_Events");
        });

        modelBuilder.Entity<Exchange>(entity =>
        {
            entity.Property(e => e.exchangeid).ValueGeneratedNever();
            entity.Property(e => e.rate).HasDefaultValue(1m);
        });

        modelBuilder.Entity<Expenditure>(entity =>
        {
            entity.Property(e => e.expenditureid).ValueGeneratedNever();
            entity.Property(e => e.sourcetype).HasComment("0 => 手動輸入; 1 => 自動帶入");
            entity.Property(e => e.status).HasComment("0 => 未付款; 1 => 部分付款; 2 => 已付款");

            entity.HasOne(d => d.purchase).WithMany(p => p.Expenditures).HasConstraintName("FK_Expenditures_Purchases");

            entity.HasOne(d => d.supplier).WithMany(p => p.Expenditures).HasConstraintName("FK_Expenditures_Suppliers");
        });

        modelBuilder.Entity<Expenditurecode>(entity =>
        {
            entity.Property(e => e.expenditurecodeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Expendituredetail>(entity =>
        {
            entity.Property(e => e.expendituredetailid).ValueGeneratedNever();

            entity.HasOne(d => d.accounting).WithMany(p => p.Expendituredetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Expendituredetails_Accountings");

            entity.HasOne(d => d.expenditure).WithMany(p => p.Expendituredetails).HasConstraintName("FK_Expendituredetails_Expenditures");
        });

        modelBuilder.Entity<GlobalMyB2B>(entity =>
        {
            entity.Property(e => e.globalmyb2bid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Income>(entity =>
        {
            entity.Property(e => e.incomeid).ValueGeneratedNever();

            entity.HasOne(d => d.member).WithMany(p => p.Incomes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Incomes_Members");
        });

        modelBuilder.Entity<Incomecode>(entity =>
        {
            entity.Property(e => e.incomecodeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.Property(e => e.invoiceid).ValueGeneratedNever();

            entity.HasOne(d => d.income).WithMany(p => p.Invoices).HasConstraintName("FK_Invoices_Incomes");

            entity.HasOne(d => d.member).WithMany(p => p.Invoices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Invoices_Members");
        });

        modelBuilder.Entity<Invoicecode>(entity =>
        {
            entity.Property(e => e.invoicecodeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Invoicedetail>(entity =>
        {
            entity.Property(e => e.invoicedetailid).ValueGeneratedNever();

            entity.HasOne(d => d.accounting).WithMany(p => p.Invoicedetails).HasConstraintName("FK_Invoicedetails_Accountings");

            entity.HasOne(d => d.invoice).WithMany(p => p.Invoicedetails).HasConstraintName("FK_Invoicedetails_Invoices");

            entity.HasOne(d => d.order).WithMany(p => p.Invoicedetails).HasConstraintName("FK_Invoicedetails_Orders");
        });

        modelBuilder.Entity<Issue>(entity =>
        {
            entity.Property(e => e.issueid).ValueGeneratedNever();
            entity.Property(e => e.intro).HasComment("說明");

            entity.HasMany(d => d.products).WithMany(p => p.issues)
                .UsingEntity<Dictionary<string, object>>(
                    "Issueproduct",
                    r => r.HasOne<Product>().WithMany()
                        .HasForeignKey("productid")
                        .HasConstraintName("FK_Issueproducts_Products"),
                    l => l.HasOne<Issue>().WithMany()
                        .HasForeignKey("issueid")
                        .HasConstraintName("FK_Issueproducts_Issues"),
                    j =>
                    {
                        j.HasKey("issueid", "productid");
                        j.ToTable("Issueproducts");
                    });

            entity.HasMany(d => d.recipes).WithMany(p => p.issues)
                .UsingEntity<Dictionary<string, object>>(
                    "Issuerecipe",
                    r => r.HasOne<Recipe>().WithMany()
                        .HasForeignKey("recipeid")
                        .HasConstraintName("FK_Issuerecipes_Recipes"),
                    l => l.HasOne<Issue>().WithMany()
                        .HasForeignKey("issueid")
                        .HasConstraintName("FK_Issuerecipes_Issues"),
                    j =>
                    {
                        j.HasKey("issueid", "recipeid");
                        j.ToTable("Issuerecipes");
                    });
        });

        modelBuilder.Entity<Issuemedia>(entity =>
        {
            entity.Property(e => e.issuemediaid).ValueGeneratedNever();

            entity.HasOne(d => d.issue).WithMany(p => p.Issuemedia).HasConstraintName("FK_Issuemedias_Issues");
        });

        modelBuilder.Entity<Knowledge>(entity =>
        {
            entity.Property(e => e.knowledgeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Knowledgemedia>(entity =>
        {
            entity.Property(e => e.knowledgemediaid).ValueGeneratedNever();

            entity.HasOne(d => d.knowledge).WithMany(p => p.Knowledgemedia).HasConstraintName("FK_Knowledgemedias_Knowledges");
        });

        modelBuilder.Entity<Lim>(entity =>
        {
            entity.Property(e => e.LimID).ValueGeneratedNever();

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent).HasConstraintName("FK_Lims_Lims");
        });

        modelBuilder.Entity<Logistic>(entity =>
        {
            entity.Property(e => e.logisticid).ValueGeneratedNever();
            entity.Property(e => e.isenable).HasDefaultValue(true);
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.Property(e => e.memberid).ValueGeneratedNever();
            entity.Property(e => e.agentdiscount).HasDefaultValue(1m);
            entity.Property(e => e.ismember)
                .HasDefaultValue(1)
                .HasComment("1 => 是會員; 2 => 僅客戶還無法網購");

            entity.HasOne(d => d.zipcode).WithMany(p => p.Members).HasConstraintName("FK_Members_Zipcodes");

            entity.HasMany(d => d.products).WithMany(p => p.members)
                .UsingEntity<Dictionary<string, object>>(
                    "Memberproduct",
                    r => r.HasOne<Product>().WithMany()
                        .HasForeignKey("productid")
                        .HasConstraintName("FK_Memberproducts_Products"),
                    l => l.HasOne<Member>().WithMany()
                        .HasForeignKey("memberid")
                        .HasConstraintName("FK_Memberproducts_Members"),
                    j =>
                    {
                        j.HasKey("memberid", "productid");
                        j.ToTable("Memberproducts");
                    });
        });

        modelBuilder.Entity<Newmedia>(entity =>
        {
            entity.Property(e => e.newmediaid).ValueGeneratedNever();

            entity.HasOne(d => d._new).WithMany(p => p.Newmedia).HasConstraintName("FK_Newmedias_News");
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.Property(e => e.newid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(e => e.orderid).ValueGeneratedNever();
            entity.Property(e => e.Click_ID).HasComment("美安用");
            entity.Property(e => e.RID).HasComment("美安用");
            entity.Property(e => e.codeatm).HasComment("虛擬帳號");
            entity.Property(e => e.companynumber).HasComment("統一編號");
            entity.Property(e => e.companytitle).HasComment("公司抬頭");
            entity.Property(e => e.deliverdate).HasComment("出貨日期");
            entity.Property(e => e.deliverstatus).HasComment("出貨狀態");
            entity.Property(e => e.discount).HasDefaultValue(0);
            entity.Property(e => e.freight).HasComment("運費");
            entity.Property(e => e.invoicecode).HasComment("發票號碼");
            entity.Property(e => e.invoicetype).HasDefaultValue(1);
            entity.Property(e => e.isdeclaration).HasComment("是否申報衛生局");
            entity.Property(e => e.ordercode).HasComment("T201609010001");
            entity.Property(e => e.ordertype).HasDefaultValue(1);
            entity.Property(e => e.paystatus).HasComment("付款狀態 未付款 => 0; 已付款 => 1; 退款 => 2; 免付款 => 3; 取消 => 4");
            entity.Property(e => e.paytype)
                .HasDefaultValue(1)
                .HasComment("付款方式");
            entity.Property(e => e.recivertime).HasDefaultValue(1);
            entity.Property(e => e.total).HasComment("總金額");
            entity.Property(e => e.trackingnumber).HasComment("物流編號");
            entity.Property(e => e.warehousetypeid).HasDefaultValue(1);

            entity.HasOne(d => d.discountNavigation).WithMany(p => p.Orders).HasConstraintName("FK_Orders_Discounts");

            entity.HasOne(d => d.logistic).WithMany(p => p.Orders).HasConstraintName("FK_Orders_Logistics");

            entity.HasOne(d => d.member).WithMany(p => p.Orders)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Members");

            entity.HasOne(d => d.reciverzipcode).WithMany(p => p.Orders)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Zipcodes");

            entity.HasOne(d => d.warehouse).WithMany(p => p.Orders).HasConstraintName("FK_Orders_Warehouses");
        });

        modelBuilder.Entity<Ordercode>(entity =>
        {
            entity.Property(e => e.ordercodeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Orderdetail>(entity =>
        {
            entity.Property(e => e.orderdetailid).ValueGeneratedNever();
            entity.Property(e => e.isgift).HasComment("0=>不是; 1=>是贈品");
            entity.Property(e => e.qty).HasDefaultValue(1);
            entity.Property(e => e.status).HasComment("是否有退貨 0=>沒退貨; 1=>退貨;");

            entity.HasOne(d => d.order).WithMany(p => p.Orderdetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orderdetails_Orders");

            entity.HasOne(d => d.product).WithMany(p => p.Orderdetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orderdetails_Products");
        });

        modelBuilder.Entity<Orderdetailstock>(entity =>
        {
            entity.Property(e => e.orderdetailstockid).ValueGeneratedNever();

            entity.HasOne(d => d.orderdetail).WithMany(p => p.Orderdetailstocks).HasConstraintName("FK_Orderdetailstocks_Orderdetails");

            entity.HasOne(d => d.warehousestock).WithMany(p => p.Orderdetailstocks)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orderdetailstocks_Warehousestocks");
        });

        modelBuilder.Entity<Outcome>(entity =>
        {
            entity.Property(e => e.outcomeid).ValueGeneratedNever();

            entity.HasOne(d => d.expenditure).WithMany(p => p.Outcomes).HasConstraintName("FK_Outcomes_Expenditures");
        });

        modelBuilder.Entity<Outcomecode>(entity =>
        {
            entity.Property(e => e.outcomecodeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Outofnotice>(entity =>
        {
            entity.Property(e => e.outofnoticeid).ValueGeneratedNever();

            entity.HasOne(d => d.member).WithMany(p => p.Outofnotices).HasConstraintName("FK_Outofnotices_Members");

            entity.HasOne(d => d.product).WithMany(p => p.Outofnotices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Outofnotices_Products");
        });

        modelBuilder.Entity<Preorder>(entity =>
        {
            entity.Property(e => e.preorderid).ValueGeneratedNever();
            entity.Property(e => e.invoicetype).HasDefaultValue(1);
            entity.Property(e => e.paytype).HasDefaultValue(1);
            entity.Property(e => e.recivertime).HasDefaultValue(1);
        });

        modelBuilder.Entity<Preorderdetail>(entity =>
        {
            entity.Property(e => e.preorderdetailid).ValueGeneratedNever();
            entity.Property(e => e.qty).HasDefaultValue(1);

            entity.HasOne(d => d.preorder).WithMany(p => p.Preorderdetails).HasConstraintName("FK_Preorderdetails_Preorders");

            entity.HasOne(d => d.product).WithMany(p => p.Preorderdetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Preorderdetails_Products");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.productid).ValueGeneratedNever();
            entity.Property(e => e.added).HasComment("上架數");
            entity.Property(e => e.capacity).HasComment("容量");
            entity.Property(e => e.conversion).HasDefaultValue(0);
            entity.Property(e => e.isdisabled).HasComment("是否下架");
            entity.Property(e => e.ishot).HasComment("熱銷商品");
            entity.Property(e => e.weight).HasComment("重量(KG)");

            entity.HasOne(d => d.brand).WithMany(p => p.Products)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Brands");

            entity.HasOne(d => d.producttype).WithMany(p => p.Products)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Producttypes");

            entity.HasMany(d => d.tags).WithMany(p => p.products)
                .UsingEntity<Dictionary<string, object>>(
                    "Producttag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("tagid")
                        .HasConstraintName("FK_Producttags_Tags"),
                    l => l.HasOne<Product>().WithMany()
                        .HasForeignKey("productid")
                        .HasConstraintName("FK_Producttags_Products"),
                    j =>
                    {
                        j.HasKey("productid", "tagid");
                        j.ToTable("Producttags");
                    });
        });

        modelBuilder.Entity<Productmedia>(entity =>
        {
            entity.Property(e => e.productmediaid).ValueGeneratedNever();

            entity.HasOne(d => d.product).WithMany(p => p.Productmedia).HasConstraintName("FK_Productmedias_Products");
        });

        modelBuilder.Entity<Productphoto>(entity =>
        {
            entity.Property(e => e.productphotoid).ValueGeneratedNever();

            entity.HasOne(d => d.product).WithMany(p => p.Productphotos).HasConstraintName("FK_Productphotos_Products");
        });

        modelBuilder.Entity<Producttype>(entity =>
        {
            entity.Property(e => e.producttypeid).ValueGeneratedNever();
            entity.Property(e => e.isenable).HasDefaultValue(true);
        });

        modelBuilder.Entity<Promote>(entity =>
        {
            entity.Property(e => e.promoteid).ValueGeneratedNever();
            entity.Property(e => e.type).HasComment("0 => 折扣%; 1 => 金額; 2 => 扣金額");
        });

        modelBuilder.Entity<Promoteproduct>(entity =>
        {
            entity.Property(e => e.promoteproductid).ValueGeneratedNever();

            entity.HasOne(d => d.product).WithMany(p => p.Promoteproducts).HasConstraintName("FK_Promoteproducts_Products");

            entity.HasOne(d => d.promote).WithMany(p => p.Promoteproducts).HasConstraintName("FK_Promoteproducts_Promotes");
        });

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.Property(e => e.purchaseid).ValueGeneratedNever();
            entity.Property(e => e.status)
                .HasDefaultValue(1)
                .HasComment("1=>未入庫; 2=>已入庫; 3=>部分入庫");

            entity.HasOne(d => d.exchange).WithMany(p => p.Purchases)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchases_Exchanges");

            entity.HasOne(d => d.supplier).WithMany(p => p.Purchases)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchases_Suppliers");
        });

        modelBuilder.Entity<Purchasecode>(entity =>
        {
            entity.Property(e => e.purchasecodeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Purchasedetail>(entity =>
        {
            entity.Property(e => e.purchasedetailid).ValueGeneratedNever();
            entity.Property(e => e.status).HasComment("0=>未入庫; 1=>已入庫; 2=>缺; 3=>多");

            entity.HasOne(d => d.product).WithMany(p => p.Purchasedetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchasedetails_Products");

            entity.HasOne(d => d.purchase).WithMany(p => p.Purchasedetails).HasConstraintName("FK_Purchasedetails_Purchases");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.Property(e => e.questionid).ValueGeneratedNever();

            entity.HasOne(d => d.questiontype).WithMany(p => p.Questions).HasConstraintName("FK_Questions_Questiontypes");
        });

        modelBuilder.Entity<Questionmedia>(entity =>
        {
            entity.Property(e => e.questionmediaid).ValueGeneratedNever();

            entity.HasOne(d => d.question).WithMany(p => p.Questionmedia).HasConstraintName("FK_Questionmedias_Questions");
        });

        modelBuilder.Entity<Questiontype>(entity =>
        {
            entity.Property(e => e.questiontypeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.Property(e => e.recipeid).ValueGeneratedNever();

            entity.HasMany(d => d.products).WithMany(p => p.recipes)
                .UsingEntity<Dictionary<string, object>>(
                    "Recipeproduct",
                    r => r.HasOne<Product>().WithMany()
                        .HasForeignKey("productid")
                        .HasConstraintName("FK_Recipeproducts_Products"),
                    l => l.HasOne<Recipe>().WithMany()
                        .HasForeignKey("recipeid")
                        .HasConstraintName("FK_Recipeproducts_Recipes"),
                    j =>
                    {
                        j.HasKey("recipeid", "productid");
                        j.ToTable("Recipeproducts");
                    });
        });

        modelBuilder.Entity<Recipeingredient>(entity =>
        {
            entity.Property(e => e.recipeingredientid).ValueGeneratedNever();

            entity.HasOne(d => d.recipe).WithMany(p => p.Recipeingredients).HasConstraintName("FK_Recipeingredients_Recipes");
        });

        modelBuilder.Entity<Recipeseasoning>(entity =>
        {
            entity.Property(e => e.recipeseasoningid).ValueGeneratedNever();

            entity.HasOne(d => d.recipe).WithMany(p => p.Recipeseasonings).HasConstraintName("FK_Recipeseasonings_Recipes");
        });

        modelBuilder.Entity<Recipestep>(entity =>
        {
            entity.Property(e => e.recipestepid).ValueGeneratedNever();

            entity.HasOne(d => d.recipe).WithMany(p => p.Recipesteps).HasConstraintName("FK_Recipesteps_Recipes");
        });

        modelBuilder.Entity<Refound>(entity =>
        {
            entity.Property(e => e.refoundid).ValueGeneratedNever();

            entity.HasOne(d => d.member).WithMany(p => p.Refounds)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Refounds_Members");

            entity.HasOne(d => d._return).WithMany(p => p.Refounds)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Refounds_Returns");
        });

        modelBuilder.Entity<Refoundcode>(entity =>
        {
            entity.Property(e => e.refoundcodeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Return>(entity =>
        {
            entity.Property(e => e.returnid).ValueGeneratedNever();
            entity.Property(e => e.receivestatus).HasComment("收貨狀態 退貨中 => 0; 已到達 => 1; 取消 => 2; 免退回 => 3");
            entity.Property(e => e.refundstatus).HasComment("0 => 未退款; 1 => 已退款; 2 => 折讓; 3 => 免退款; 4 => 取消");

            entity.HasOne(d => d.member).WithMany(p => p.Returns)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Returns_Members");

            entity.HasOne(d => d.order).WithMany(p => p.Returns)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Returns_Orders");
        });

        modelBuilder.Entity<Returncode>(entity =>
        {
            entity.Property(e => e.returncodeid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Returndetail>(entity =>
        {
            entity.Property(e => e.returndetailid).ValueGeneratedNever();

            entity.HasOne(d => d.accounting).WithMany(p => p.Returndetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Returndetails_Accountings");

            entity.HasOne(d => d.orderdetail).WithMany(p => p.Returndetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Returndetails_Orderdetails");

            entity.HasOne(d => d._return).WithMany(p => p.Returndetails).HasConstraintName("FK_Returndetails_Returns");
        });

        modelBuilder.Entity<Setproduct>(entity =>
        {
            entity.Property(e => e.qty).HasDefaultValue(1);

            entity.HasOne(d => d.oproduct).WithMany(p => p.Setproductoproducts).HasConstraintName("FK_Setproducts_Products");

            entity.HasOne(d => d.product).WithMany(p => p.Setproductproducts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Setproducts_Products1");
        });

        modelBuilder.Entity<Sm>(entity =>
        {
            entity.Property(e => e.smsid).ValueGeneratedNever();
            entity.Property(e => e.dlvtime).HasComment("預約發送時間");
            entity.Property(e => e.smbody).HasComment("簡訊內容");
        });

        modelBuilder.Entity<Smsdetail>(entity =>
        {
            entity.Property(e => e.smsdetailid).ValueGeneratedNever();

            entity.HasOne(d => d.member).WithMany(p => p.Smsdetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Smsdetails_Members");

            entity.HasOne(d => d.sms).WithMany(p => p.Smsdetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Smsdetails_Sms");
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.Property(e => e.stockid).ValueGeneratedNever();
            entity.Property(e => e.createdate).HasComment("入庫日期");
            entity.Property(e => e.declarationnumber).HasComment("批號");
            entity.Property(e => e.expiredate).HasComment("到期日");
            entity.Property(e => e.noticenumber).HasComment("輸入許可通知號碼");
            entity.Property(e => e.quantity).HasComment("pce");
            entity.Property(e => e.status)
                .HasDefaultValue(1)
                .HasComment("1=>合格; 2=>不合格待複檢");
            entity.Property(e => e.stocktype)
                .HasDefaultValue(1)
                .HasComment("1 => 需申報; 2 => 不需申報");
            entity.Property(e => e.weight)
                .HasDefaultValue(0m)
                .HasComment("淨重");

            entity.HasOne(d => d.purchasedetail).WithMany(p => p.Stocks)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Stocks_Purchasedetails");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.Property(e => e.supplierid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.Property(e => e.tagid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Viewlog>(entity =>
        {
            entity.Property(e => e.viewlogid).ValueGeneratedNever();
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.Property(e => e.warehouseid).ValueGeneratedNever();
            entity.Property(e => e.warehousetype).HasDefaultValue(1);
        });

        modelBuilder.Entity<Warehousestock>(entity =>
        {
            entity.Property(e => e.warehousestockid).ValueGeneratedNever();

            entity.HasOne(d => d.stock).WithMany(p => p.Warehousestocks)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Warehousestocks_Stocks");
        });

        modelBuilder.Entity<Zipcode>(entity =>
        {
            entity.Property(e => e.countryid).HasDefaultValue(1);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
