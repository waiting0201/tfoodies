using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Income
{
    [Key]
    public Guid incomeid { get; set; }

    public Guid memberid { get; set; }

    [StringLength(15)]
    public string incomecode { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime incomedate { get; set; }

    public int amount { get; set; }

    public int fee { get; set; }

    [Column(TypeName = "ntext")]
    public string? note { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [InverseProperty("income")]
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    [ForeignKey("memberid")]
    [InverseProperty("Incomes")]
    public virtual Member member { get; set; } = null!;
}
