namespace MarsReferral.Web.Models;
public sealed class Pagination
{
    public int Total { get; }
    public int PageSize { get; }
    public int PageNumber { get; }
    public int PageCount { get; }
    public int Skip => (PageNumber - 1) * PageSize;
    public int First => Total == 0 ? 0 : Skip + 1;
    public int Last => Math.Min(Skip + PageSize, Total);
    public Pagination(int total, int page, int pageSize)
    {
        Total = total;
        PageSize = pageSize is 10 or 25 or 50 ? pageSize : 10;
        PageCount = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        PageNumber = Math.Clamp(page, 1, PageCount);
    }
}
