namespace back_end.Dtos
{
  public class TableGroupAssignmentDto
  {
    public int TableGroup_Id { get; set; }
  }

  public class TableGroupCreateDto
  {
    public string Group_Name { get; set; } = string.Empty;
    public bool? Is_Active { get; set; }
    public int Location_Id { get; set; }
  }

  public class TableGroupUpdateDto
  {
    public string? Group_Name { get; set; }
    public bool? Is_Active { get; set; }
  }
}