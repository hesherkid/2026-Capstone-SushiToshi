using System.ComponentModel.DataAnnotations.Schema;

namespace back_end.domain.Entities
{
    // A menu item a signed-in customer has hearted. One row per customer + item (composite key)
    [Table("favourite_item")]
    public class FavouriteItem
    {
        public int User_Id { get; set; }

        public int Item_Id { get; set; }

        public DateTime Created_At { get; set; }

        public User User { get; set; } = null!;
        public Menu_Item MenuItem { get; set; } = null!;
    }
}
