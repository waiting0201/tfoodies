using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Expendituredetail
{
    [Key]
    public Guid expendituredetailid { get; set; }

    public Guid expenditureid { get; set; }

    public Guid accountingid { get; set; }

    public Guid? purchasedetailid { get; set; }

    [Column(TypeName = "ntext")]
    public string? summary { get; set; }

    public int price { get; set; }

    [ForeignKey("accountingid")]
    [InverseProperty("Expendituredetails")]
    public virtual Accounting accounting { get; set; } = null!;

    [ForeignKey("expenditureid")]
    [InverseProperty("Expendituredetails")]
    public virtual Expenditure expenditure { get; set; } = null!;
}
