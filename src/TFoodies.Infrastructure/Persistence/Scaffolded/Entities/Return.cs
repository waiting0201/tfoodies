using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Return
{
    [Key]
    public Guid returnid { get; set; }

    public Guid memberid { get; set; }

    public Guid orderid { get; set; }

    [StringLength(15)]
    public string returncode { get; set; } = null!;

    public DateOnly returndate { get; set; }

    /// <summary>
    /// 收貨狀態 退貨中 =&gt; 0; 已到達 =&gt; 1; 取消 =&gt; 2; 免退回 =&gt; 3
    /// </summary>
    public int receivestatus { get; set; }

    public DateOnly? receivedate { get; set; }

    /// <summary>
    /// 0 =&gt; 未退款; 1 =&gt; 已退款; 2 =&gt; 折讓; 3 =&gt; 免退款; 4 =&gt; 取消
    /// </summary>
    public int refundstatus { get; set; }

    public DateOnly? refunddate { get; set; }

    public DateOnly createdate { get; set; }

    public int warehousestatus { get; set; }

    public DateOnly? warehousesdate { get; set; }

    [Column(TypeName = "ntext")]
    public string? note { get; set; }

    [InverseProperty("_return")]
    public virtual ICollection<Refound> Refounds { get; set; } = new List<Refound>();

    [InverseProperty("_return")]
    public virtual ICollection<Returndetail> Returndetails { get; set; } = new List<Returndetail>();

    [ForeignKey("memberid")]
    [InverseProperty("Returns")]
    public virtual Member member { get; set; } = null!;

    [ForeignKey("orderid")]
    [InverseProperty("Returns")]
    public virtual Order order { get; set; } = null!;
}
