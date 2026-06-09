using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Order
{
    [Key]
    public Guid orderid { get; set; }

    public Guid memberid { get; set; }

    public int ordertype { get; set; }

    public int warehousetypeid { get; set; }

    public Guid? warehouseid { get; set; }

    public Guid? logisticid { get; set; }

    /// <summary>
    /// T201609010001
    /// </summary>
    [StringLength(15)]
    public string ordercode { get; set; } = null!;

    public DateOnly orderdate { get; set; }

    [StringLength(20)]
    public string recivername { get; set; } = null!;

    [StringLength(15)]
    public string recivermobile { get; set; } = null!;

    public int reciverzipcodeid { get; set; }

    [StringLength(150)]
    public string reciveraddress { get; set; } = null!;

    public int recivertime { get; set; }

    /// <summary>
    /// 運費
    /// </summary>
    public int freight { get; set; }

    public int? discount { get; set; }

    /// <summary>
    /// 總金額
    /// </summary>
    public int total { get; set; }

    /// <summary>
    /// 付款方式
    /// </summary>
    public int paytype { get; set; }

    public int invoicetype { get; set; }

    /// <summary>
    /// 發票號碼
    /// </summary>
    [StringLength(150)]
    public string? invoicecode { get; set; }

    public int invoicestatus { get; set; }

    /// <summary>
    /// 公司抬頭
    /// </summary>
    [StringLength(50)]
    public string? companytitle { get; set; }

    /// <summary>
    /// 統一編號
    /// </summary>
    [StringLength(8)]
    public string? companynumber { get; set; }

    /// <summary>
    /// 虛擬帳號
    /// </summary>
    [StringLength(16)]
    public string? codeatm { get; set; }

    public DateOnly? expirepaydate { get; set; }

    [Column(TypeName = "ntext")]
    public string? remark { get; set; }

    [StringLength(250)]
    public string? note { get; set; }

    /// <summary>
    /// 付款狀態 未付款 =&gt; 0; 已付款 =&gt; 1; 退款 =&gt; 2; 免付款 =&gt; 3; 取消 =&gt; 4
    /// </summary>
    public int paystatus { get; set; }

    public DateOnly? paydate { get; set; }

    /// <summary>
    /// 出貨狀態
    /// </summary>
    public int deliverstatus { get; set; }

    /// <summary>
    /// 出貨日期
    /// </summary>
    public DateOnly? deliverdate { get; set; }

    /// <summary>
    /// 物流編號
    /// </summary>
    [StringLength(25)]
    public string? trackingnumber { get; set; }

    [StringLength(10)]
    public string? lovecode { get; set; }

    public Guid? discountid { get; set; }

    [StringLength(4)]
    public string? lastpan4 { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    /// <summary>
    /// 是否申報衛生局
    /// </summary>
    public bool isdeclaration { get; set; }

    /// <summary>
    /// 美安用
    /// </summary>
    [StringLength(255)]
    public string? RID { get; set; }

    /// <summary>
    /// 美安用
    /// </summary>
    [StringLength(255)]
    public string? Click_ID { get; set; }

    [InverseProperty("order")]
    public virtual ICollection<Declarationdetail> Declarationdetails { get; set; } = new List<Declarationdetail>();

    [InverseProperty("order")]
    public virtual ICollection<Invoicedetail> Invoicedetails { get; set; } = new List<Invoicedetail>();

    [InverseProperty("order")]
    public virtual ICollection<Orderdetail> Orderdetails { get; set; } = new List<Orderdetail>();

    [InverseProperty("order")]
    public virtual ICollection<Return> Returns { get; set; } = new List<Return>();

    [ForeignKey("discountid")]
    [InverseProperty("Orders")]
    public virtual Discount? discountNavigation { get; set; }

    [ForeignKey("logisticid")]
    [InverseProperty("Orders")]
    public virtual Logistic? logistic { get; set; }

    [ForeignKey("memberid")]
    [InverseProperty("Orders")]
    public virtual Member member { get; set; } = null!;

    [ForeignKey("reciverzipcodeid")]
    [InverseProperty("Orders")]
    public virtual Zipcode reciverzipcode { get; set; } = null!;

    [ForeignKey("warehouseid")]
    [InverseProperty("Orders")]
    public virtual Warehouse? warehouse { get; set; }
}
