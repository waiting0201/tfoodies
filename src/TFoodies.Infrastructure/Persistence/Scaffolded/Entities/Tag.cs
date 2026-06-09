using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Tag
{
    [Key]
    public Guid tagid { get; set; }

    [StringLength(50)]
    public string title { get; set; } = null!;

    [ForeignKey("tagid")]
    [InverseProperty("tags")]
    public virtual ICollection<Product> products { get; set; } = new List<Product>();
}
