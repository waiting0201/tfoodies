using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Accounting
{
    [Key]
    public Guid accountingid { get; set; }

    [StringLength(6)]
    public string accountingcode { get; set; } = null!;

    [StringLength(50)]
    public string title { get; set; } = null!;

    [InverseProperty("accounting")]
    public virtual ICollection<Expendituredetail> Expendituredetails { get; set; } = new List<Expendituredetail>();

    [InverseProperty("accounting")]
    public virtual ICollection<Invoicedetail> Invoicedetails { get; set; } = new List<Invoicedetail>();

    [InverseProperty("accounting")]
    public virtual ICollection<Returndetail> Returndetails { get; set; } = new List<Returndetail>();
}
