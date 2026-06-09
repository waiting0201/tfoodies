using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Supplier
{
    [Key]
    public Guid supplierid { get; set; }

    [StringLength(80)]
    public string title { get; set; } = null!;

    [StringLength(35)]
    public string contactor { get; set; } = null!;

    [StringLength(200)]
    public string address { get; set; } = null!;

    [StringLength(50)]
    public string phone { get; set; } = null!;

    [InverseProperty("supplier")]
    public virtual ICollection<Brand> Brands { get; set; } = new List<Brand>();

    [InverseProperty("supplier")]
    public virtual ICollection<Expenditure> Expenditures { get; set; } = new List<Expenditure>();

    [InverseProperty("supplier")]
    public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}
