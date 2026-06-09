using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Product
{
    [Key]
    public Guid productid { get; set; }

    public Guid producttypeid { get; set; }

    public Guid brandid { get; set; }

    [StringLength(20)]
    public string? productnum { get; set; }

    [StringLength(100)]
    public string title { get; set; } = null!;

    [StringLength(100)]
    public string? entitle { get; set; }

    [Column(TypeName = "ntext")]
    public string? intro { get; set; }

    [Column(TypeName = "ntext")]
    public string memo { get; set; } = null!;

    public int? fixprice { get; set; }

    public int price { get; set; }

    /// <summary>
    /// 容量
    /// </summary>
    [StringLength(50)]
    public string? capacity { get; set; }

    [StringLength(50)]
    public string photo { get; set; } = null!;

    /// <summary>
    /// 上架數
    /// </summary>
    public int added { get; set; }

    /// <summary>
    /// 熱銷商品
    /// </summary>
    public bool ishot { get; set; }

    public bool isnew { get; set; }

    /// <summary>
    /// 是否下架
    /// </summary>
    public bool isdisabled { get; set; }

    [StringLength(150)]
    public string? keyword { get; set; }

    [StringLength(200)]
    public string? description { get; set; }

    [StringLength(10)]
    public string? unit { get; set; }

    public int? conversion { get; set; }

    /// <summary>
    /// 重量(KG)
    /// </summary>
    [Column(TypeName = "decimal(6, 3)")]
    public decimal? weight { get; set; }

    public bool isset { get; set; }

    public bool isgroupbuy { get; set; }

    public int sort { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [StringLength(150)]
    public string? shortener { get; set; }

    [InverseProperty("product")]
    public virtual ICollection<Orderdetail> Orderdetails { get; set; } = new List<Orderdetail>();

    [InverseProperty("product")]
    public virtual ICollection<Outofnotice> Outofnotices { get; set; } = new List<Outofnotice>();

    [InverseProperty("product")]
    public virtual ICollection<Preorderdetail> Preorderdetails { get; set; } = new List<Preorderdetail>();

    [InverseProperty("product")]
    public virtual ICollection<Productmedia> Productmedia { get; set; } = new List<Productmedia>();

    [InverseProperty("product")]
    public virtual ICollection<Productphoto> Productphotos { get; set; } = new List<Productphoto>();

    [InverseProperty("product")]
    public virtual ICollection<Promoteproduct> Promoteproducts { get; set; } = new List<Promoteproduct>();

    [InverseProperty("product")]
    public virtual ICollection<Purchasedetail> Purchasedetails { get; set; } = new List<Purchasedetail>();

    [InverseProperty("oproduct")]
    public virtual ICollection<Setproduct> Setproductoproducts { get; set; } = new List<Setproduct>();

    [InverseProperty("product")]
    public virtual ICollection<Setproduct> Setproductproducts { get; set; } = new List<Setproduct>();

    [ForeignKey("brandid")]
    [InverseProperty("Products")]
    public virtual Brand brand { get; set; } = null!;

    [ForeignKey("producttypeid")]
    [InverseProperty("Products")]
    public virtual Producttype producttype { get; set; } = null!;

    [ForeignKey("productid")]
    [InverseProperty("products")]
    public virtual ICollection<Issue> issues { get; set; } = new List<Issue>();

    [ForeignKey("productid")]
    [InverseProperty("products")]
    public virtual ICollection<Member> members { get; set; } = new List<Member>();

    [ForeignKey("productid")]
    [InverseProperty("products")]
    public virtual ICollection<Recipe> recipes { get; set; } = new List<Recipe>();

    [ForeignKey("productid")]
    [InverseProperty("products")]
    public virtual ICollection<Tag> tags { get; set; } = new List<Tag>();
}
