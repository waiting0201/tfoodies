using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Member
{
    [Key]
    public Guid memberid { get; set; }

    [StringLength(20)]
    public string name { get; set; } = null!;

    [StringLength(15)]
    public string mobile { get; set; } = null!;

    public int? gender { get; set; }

    public DateOnly? birthday { get; set; }

    [StringLength(20)]
    public string password { get; set; } = null!;

    [StringLength(150)]
    public string? email { get; set; }

    public int? zipcodeid { get; set; }

    [StringLength(150)]
    public string? address { get; set; }

    public bool isagent { get; set; }

    [Column(TypeName = "decimal(3, 2)")]
    public decimal agentdiscount { get; set; }

    [Column(TypeName = "ntext")]
    public string? memo { get; set; }

    public DateOnly createdate { get; set; }

    /// <summary>
    /// 1 =&gt; 是會員; 2 =&gt; 僅客戶還無法網購
    /// </summary>
    public int ismember { get; set; }

    public bool isenable { get; set; }

    [InverseProperty("member")]
    public virtual ICollection<Income> Incomes { get; set; } = new List<Income>();

    [InverseProperty("member")]
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    [InverseProperty("member")]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    [InverseProperty("member")]
    public virtual ICollection<Outofnotice> Outofnotices { get; set; } = new List<Outofnotice>();

    [InverseProperty("member")]
    public virtual ICollection<Refound> Refounds { get; set; } = new List<Refound>();

    [InverseProperty("member")]
    public virtual ICollection<Return> Returns { get; set; } = new List<Return>();

    [InverseProperty("member")]
    public virtual ICollection<Smsdetail> Smsdetails { get; set; } = new List<Smsdetail>();

    [ForeignKey("zipcodeid")]
    [InverseProperty("Members")]
    public virtual Zipcode? zipcode { get; set; }

    [ForeignKey("memberid")]
    [InverseProperty("members")]
    public virtual ICollection<Product> products { get; set; } = new List<Product>();
}
