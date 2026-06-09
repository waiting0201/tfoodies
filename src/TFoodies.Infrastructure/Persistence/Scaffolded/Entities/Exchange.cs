using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Exchange
{
    [Key]
    public Guid exchangeid { get; set; }

    [StringLength(10)]
    public string title { get; set; } = null!;

    [Column(TypeName = "decimal(10, 2)")]
    public decimal rate { get; set; }

    [InverseProperty("exchange")]
    public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}
