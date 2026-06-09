using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Banner
{
    [Key]
    public Guid bannerid { get; set; }

    [StringLength(150)]
    public string? title { get; set; }

    [StringLength(50)]
    public string? subtitle { get; set; }

    [StringLength(250)]
    public string? url { get; set; }

    [StringLength(50)]
    public string photo { get; set; } = null!;

    public int style { get; set; }

    public int sort { get; set; }
}
