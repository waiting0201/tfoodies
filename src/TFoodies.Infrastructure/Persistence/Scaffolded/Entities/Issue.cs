using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Issue
{
    [Key]
    public Guid issueid { get; set; }

    [StringLength(80)]
    public string title { get; set; } = null!;

    [StringLength(50)]
    public string photo { get; set; } = null!;

    /// <summary>
    /// 說明
    /// </summary>
    [Column(TypeName = "ntext")]
    public string? intro { get; set; }

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

    [InverseProperty("issue")]
    public virtual ICollection<Issuemedia> Issuemedia { get; set; } = new List<Issuemedia>();

    [ForeignKey("issueid")]
    [InverseProperty("issues")]
    public virtual ICollection<Product> products { get; set; } = new List<Product>();

    [ForeignKey("issueid")]
    [InverseProperty("issues")]
    public virtual ICollection<Recipe> recipes { get; set; } = new List<Recipe>();
}
