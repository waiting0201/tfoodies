using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Questionmedia
{
    [Key]
    public Guid questionmediaid { get; set; }

    public Guid questionid { get; set; }

    public int mediatype { get; set; }

    [StringLength(150)]
    public string? videourl { get; set; }

    [StringLength(50)]
    public string? filename { get; set; }

    [StringLength(250)]
    public string? filenamepath { get; set; }

    [ForeignKey("questionid")]
    [InverseProperty("Questionmedia")]
    public virtual Question question { get; set; } = null!;
}
