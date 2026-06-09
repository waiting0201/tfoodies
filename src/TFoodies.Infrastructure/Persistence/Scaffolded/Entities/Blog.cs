using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Blog
{
    [Key]
    public Guid blogid { get; set; }

    [StringLength(100)]
    public string title { get; set; } = null!;

    [StringLength(50)]
    public string photo { get; set; } = null!;

    [StringLength(250)]
    public string link { get; set; } = null!;

    public int sort { get; set; }
}
