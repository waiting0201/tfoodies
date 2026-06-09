using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Preorderdetail
{
    [Key]
    public Guid preorderdetailid { get; set; }

    public Guid preorderid { get; set; }

    public Guid productid { get; set; }

    public int qty { get; set; }

    public int price { get; set; }

    public int subtotal { get; set; }

    public int status { get; set; }

    [ForeignKey("preorderid")]
    [InverseProperty("Preorderdetails")]
    public virtual Preorder preorder { get; set; } = null!;

    [ForeignKey("productid")]
    [InverseProperty("Preorderdetails")]
    public virtual Product product { get; set; } = null!;
}
