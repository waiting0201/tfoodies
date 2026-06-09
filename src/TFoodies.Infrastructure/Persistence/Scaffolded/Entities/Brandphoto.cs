using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Brandphoto
{
    [Key]
    public Guid brandphotoid { get; set; }

    public Guid brandid { get; set; }

    [StringLength(50)]
    public string photo { get; set; } = null!;

    public int sort { get; set; }

    [ForeignKey("brandid")]
    [InverseProperty("Brandphotos")]
    public virtual Brand brand { get; set; } = null!;
}
