using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Invoicedetail
{
    [Key]
    public Guid invoicedetailid { get; set; }

    public Guid invoiceid { get; set; }

    public Guid? accountingid { get; set; }

    public Guid? orderid { get; set; }

    public int? price { get; set; }

    public int? tax { get; set; }

    [StringLength(250)]
    public string? note { get; set; }

    [ForeignKey("accountingid")]
    [InverseProperty("Invoicedetails")]
    public virtual Accounting? accounting { get; set; }

    [ForeignKey("invoiceid")]
    [InverseProperty("Invoicedetails")]
    public virtual Invoice invoice { get; set; } = null!;

    [ForeignKey("orderid")]
    [InverseProperty("Invoicedetails")]
    public virtual Order? order { get; set; }
}
