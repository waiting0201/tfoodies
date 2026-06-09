using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Invoice
{
    [Key]
    public Guid invoiceid { get; set; }

    public Guid? incomeid { get; set; }

    [StringLength(15)]
    public string invoicecode { get; set; } = null!;

    public Guid memberid { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime requestdate { get; set; }

    [Column(TypeName = "ntext")]
    public string? note { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [InverseProperty("invoice")]
    public virtual ICollection<Invoicedetail> Invoicedetails { get; set; } = new List<Invoicedetail>();

    [ForeignKey("incomeid")]
    [InverseProperty("Invoices")]
    public virtual Income? income { get; set; }

    [ForeignKey("memberid")]
    [InverseProperty("Invoices")]
    public virtual Member member { get; set; } = null!;
}
