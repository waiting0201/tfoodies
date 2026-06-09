using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Promoteproduct
{
    [Key]
    public Guid promoteproductid { get; set; }

    public Guid promoteid { get; set; }

    public Guid productid { get; set; }

    [ForeignKey("productid")]
    [InverseProperty("Promoteproducts")]
    public virtual Product product { get; set; } = null!;

    [ForeignKey("promoteid")]
    [InverseProperty("Promoteproducts")]
    public virtual Promote promote { get; set; } = null!;
}
