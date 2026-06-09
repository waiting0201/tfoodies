using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Productphoto
{
    [Key]
    public Guid productphotoid { get; set; }

    public Guid productid { get; set; }

    [StringLength(50)]
    public string photo { get; set; } = null!;

    public int sort { get; set; }

    [ForeignKey("productid")]
    [InverseProperty("Productphotos")]
    public virtual Product product { get; set; } = null!;
}
