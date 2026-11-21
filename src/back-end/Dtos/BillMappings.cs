using back_end.domain.Entities;

namespace back_end.Dtos
{
  public static class BillMappings
  {
    // session: the bill's dining session when it's loaded separately; otherwise bill.DiningSession.
    // Either way it needs Table and TableGroup.Tables loaded for the table numbers
    public static BillResponse ToBillResponse(this Billing bill, DiningSession? session = null) => new()
    {
      BillId = bill.Bill_Id,
      SessionId = bill.Session_Id,
      BillName = bill.Bill_Name,
      SeniorCount = bill.Senior_Count,
      AdultCount = bill.Adult_Count,
      ChildCount = bill.Child_Count,
      TotCount = bill.Tot_Count,
      TotalCount = bill.Total_Count,
      Status = bill.Status.ToString(),
      CreatedAt = bill.Created_At,
      ClosedAt = bill.Closed_At,
      RequestedAt = bill.Requested_At,
      TableNumbers = (session ?? bill.DiningSession).TableNumbers()
    };

    // The session's table, or every table in its group
    public static List<int> TableNumbers(this DiningSession session)
    {
      if (session.Table != null)
      {
        return new List<int> { session.Table.table_number };
      }
      if (session.TableGroup?.Tables != null)
      {
        return session.TableGroup.Tables.Select(t => t.table_number).ToList();
      }
      return new List<int>();
    }
  }
}
