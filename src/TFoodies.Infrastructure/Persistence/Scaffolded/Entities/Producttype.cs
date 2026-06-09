using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Producttype
{
    [Key]
    public Guid producttypeid { get; set; }

    [StringLength(50)]
    public string title { get; set; } = null!;

    [StringLength(150)]
    public string? keyword { get; set; }

    [StringLength(200)]
    public string? description { get; set; }

    [StringLength(255)]
    public string? memo { get; set; }

    public int sort { get; set; }

    public bool isenable { get; set; }

    [InverseProperty("producttype")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
