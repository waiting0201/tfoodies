using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Setproduct
{
    [Key]
    public int setproductid { get; set; }

    public Guid oproductid { get; set; }

    public Guid productid { get; set; }

    public int qty { get; set; }

    [ForeignKey("oproductid")]
    [InverseProperty("Setproductoproducts")]
    public virtual Product oproduct { get; set; } = null!;

    [ForeignKey("productid")]
    [InverseProperty("Setproductproducts")]
    public virtual Product product { get; set; } = null!;
}
