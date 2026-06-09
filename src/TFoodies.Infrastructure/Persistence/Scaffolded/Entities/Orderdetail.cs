using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Orderdetail
{
    [Key]
    public Guid orderdetailid { get; set; }

    public Guid orderid { get; set; }

    public Guid productid { get; set; }

    public int qty { get; set; }

    public int price { get; set; }

    public int? discount { get; set; }

    public int subtotal { get; set; }

    /// <summary>
    /// 0=&gt;不是; 1=&gt;是贈品
    /// </summary>
    public int isgift { get; set; }

    /// <summary>
    /// 是否有退貨 0=&gt;沒退貨; 1=&gt;退貨;
    /// </summary>
    public int status { get; set; }

    [InverseProperty("orderdetail")]
    public virtual ICollection<Orderdetailstock> Orderdetailstocks { get; set; } = new List<Orderdetailstock>();

    [InverseProperty("orderdetail")]
    public virtual ICollection<Returndetail> Returndetails { get; set; } = new List<Returndetail>();

    [ForeignKey("orderid")]
    [InverseProperty("Orderdetails")]
    public virtual Order order { get; set; } = null!;

    [ForeignKey("productid")]
    [InverseProperty("Orderdetails")]
    public virtual Product product { get; set; } = null!;
}
