using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using tfoodies.Models;

namespace tfoodies.Commons
{
    public class Cart
    {
        private IList<CartItem> cartitems;

        public Cart()
        {
            if(HttpContext.Current.Session["myCart"] != null)
            {
                cartitems = (List<CartItem>)HttpContext.Current.Session["myCart"];
            }
            else
            {
                cartitems = new List<CartItem>();
                HttpContext.Current.Session["myCart"] = cartitems;
            }
        }

        public CartItem FindByRowid(Guid rowid)
        {
            CartItem cartitem = cartitems.Where(a => a.RowId == rowid).FirstOrDefault();

            return cartitem;
        }

        public CartItem FindByProductid(Guid productid)
        {
            CartItem cartitem = cartitems.Where(a => a.ProductId == productid).FirstOrDefault();

            return cartitem;
        }

        public Guid Insert(CartItem item)
        {
            CartItem cartitem = cartitems.Where(a => a.ProductId == item.ProductId).FirstOrDefault();
            
            if(cartitem == null)
            {
                item.RowId = Guid.NewGuid();
                cartitems.Add(item);
            }
            else
            {
                cartitem.Quantity = cartitem.Quantity + item.Quantity;
            }
            
            HttpContext.Current.Session["myCart"] = cartitems;

            return item.RowId;
        }

        public CartItem Update(Guid rowid, int qty)
        {
            CartItem cartitem = cartitems.Where(a => a.RowId == rowid).FirstOrDefault();
            if(cartitem != null)
            {
                cartitem.Quantity = qty;
                
                HttpContext.Current.Session["myCart"] = cartitems;
            }

            return cartitem;
        }

        public IEnumerable<CartItem> UpdateAll(IEnumerable<CartItem> items)
        {
            HttpContext.Current.Session["myCart"] = items;

            return items;
        }

        public bool Remove(Guid rowid)
        {
            bool isremove = false;

            CartItem cartitem = cartitems.Where(a => a.RowId == rowid).FirstOrDefault();
            if(cartitem != null)
            {
                cartitems.Remove(cartitem);
                
                HttpContext.Current.Session["myCart"] = cartitems;

                isremove = true;
            }

            return isremove;
        }

        public IEnumerable<CartItem> Contents()
        {
            return cartitems;
        }

        public int TotalItems()
        {
            int total = 0;
            foreach(CartItem item in cartitems)
            {
                total = total + item.Quantity;
            }

            return total;
        }

        public int TotalPrice()
        {
            int total = 0;
            foreach(CartItem item in cartitems)
            {
                total = total + item.SubTotal;
            }

            return total;
        }
    }
}