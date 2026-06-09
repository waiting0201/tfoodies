using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Knowledge
{
    [Key]
    public Guid knowledgeid { get; set; }

    [StringLength(250)]
    public string question { get; set; } = null!;

    [StringLength(50)]
    public string photo { get; set; } = null!;

    [Column(TypeName = "ntext")]
    public string answer { get; set; } = null!;

    [StringLength(150)]
    public string? keyword { get; set; }

    [StringLength(200)]
    public string? description { get; set; }

    public int sort { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    public bool ispublish { get; set; }

    [StringLength(150)]
    public string? shortener { get; set; }

    [InverseProperty("knowledge")]
    public virtual ICollection<Knowledgemedia> Knowledgemedia { get; set; } = new List<Knowledgemedia>();
}
