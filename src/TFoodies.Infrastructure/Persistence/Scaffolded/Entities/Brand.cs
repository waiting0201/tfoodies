using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Brand
{
    [Key]
    public Guid brandid { get; set; }

    public Guid? supplierid { get; set; }

    [StringLength(150)]
    public string title { get; set; } = null!;

    [StringLength(50)]
    public string? subtitle { get; set; }

    [StringLength(50)]
    public string? logo { get; set; }

    [StringLength(50)]
    public string? banner { get; set; }

    [StringLength(150)]
    public string? patternentitle { get; set; }

    [StringLength(50)]
    public string? patternchtitle { get; set; }

    [StringLength(50)]
    public string? parttnervideo { get; set; }

    [Column(TypeName = "ntext")]
    public string? patternmemo { get; set; }

    [StringLength(50)]
    public string? patternclass { get; set; }

    [StringLength(50)]
    public string? ilogo { get; set; }

    [StringLength(50)]
    public string? slogan { get; set; }

    [Column(TypeName = "ntext")]
    public string? intro { get; set; }

    [StringLength(50)]
    public string? storybgclass { get; set; }

    [StringLength(50)]
    public string? storyentitle { get; set; }

    [StringLength(50)]
    public string? storychtitle { get; set; }

    [Column(TypeName = "ntext")]
    public string? storymemo { get; set; }

    [StringLength(50)]
    public string? peopletitle { get; set; }

    [StringLength(50)]
    public string? peopleslogan { get; set; }

    [Column(TypeName = "ntext")]
    public string? peoplememo { get; set; }

    [StringLength(50)]
    public string? peoplephoto { get; set; }

    [StringLength(150)]
    public string? keyword { get; set; }

    [StringLength(200)]
    public string? description { get; set; }

    public int sort { get; set; }

    public int isdisplay { get; set; }

    [InverseProperty("brand")]
    public virtual ICollection<Brandphoto> Brandphotos { get; set; } = new List<Brandphoto>();

    [InverseProperty("brand")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [ForeignKey("supplierid")]
    [InverseProperty("Brands")]
    public virtual Supplier? supplier { get; set; }
}
