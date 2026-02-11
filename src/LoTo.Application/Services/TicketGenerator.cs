namespace LoTo.Application.Services;

/// <summary>
/// Generate vé lô tô theo format Việt Nam:
/// - 9 hàng x 9 cột (3 block x 3 hàng)
/// - Mỗi hàng: 5 số, 4 ô trống (null)
/// - Tổng: 45 số (mỗi cột chọn 5 từ range)
/// - Cột 0: 1-9, Cột 1: 10-19, ..., Cột 8: 80-90
/// - Số trong mỗi cột được sắp xếp từ trên xuống
/// </summary>
public static class TicketGenerator
{
    private static readonly Random _random = new();

    private static readonly (int min, int max)[] ColumnRanges =
    [
        (1, 9), (10, 19), (20, 29), (30, 39), (40, 49),
        (50, 59), (60, 69), (70, 79), (80, 90)
    ];

    /// <summary>
    /// Generate 1 vé lô tô. Returns int?[9][9] (9 rows x 9 cols)
    /// </summary>
    public static int?[][] Generate()
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var result = TryGenerate();
            if (result != null) return result;
        }

        return GenerateFallback();
    }

    private static int?[][]? TryGenerate()
    {
        var grid = new int?[9][];
        for (var r = 0; r < 9; r++)
            grid[r] = new int?[9];

        var rowCounts = new int[9];

        // Shuffle column order để tránh bias
        var colOrder = Enumerable.Range(0, 9).OrderBy(_ => _random.Next()).ToList();

        foreach (var c in colOrder)
        {
            var (min, max) = ColumnRanges[c];
            var allNumbers = Enumerable.Range(min, max - min + 1).ToList();

            // Chọn ngẫu nhiên 5 số từ column range
            var selected = allNumbers.OrderBy(_ => _random.Next()).Take(5).ToList();
            selected.Sort();

            // Chọn 5 hàng còn chỗ (< 5 số/hàng)
            var availableRows = Enumerable.Range(0, 9)
                .Where(r => rowCounts[r] < 5)
                .OrderBy(_ => _random.Next())
                .Take(5)
                .OrderBy(r => r) // Sắp xếp để số nhỏ ở trên
                .ToList();

            if (availableRows.Count < 5)
                return null;

            for (var i = 0; i < 5; i++)
            {
                grid[availableRows[i]][c] = selected[i];
                rowCounts[availableRows[i]]++;
            }
        }

        // Verify: mỗi hàng phải có đúng 5 số
        if (rowCounts.Any(c => c != 5))
            return null;

        return grid;
    }

    private static int?[][] GenerateFallback()
    {
        var grid = new int?[9][];
        for (var r = 0; r < 9; r++)
            grid[r] = new int?[9];

        // Fallback: mỗi cột chọn 5 số đầu, phân bổ cố định
        var assignments = new (int col, int[] rows)[]
        {
            (0, [0, 1, 3, 5, 7]),
            (1, [0, 2, 4, 6, 8]),
            (2, [1, 2, 5, 7, 8]),
            (3, [0, 3, 4, 6, 7]),
            (4, [1, 3, 5, 6, 8]),
            (5, [0, 2, 4, 7, 8]),
            (6, [1, 2, 3, 5, 6]),
            (7, [0, 4, 6, 7, 8]),
            (8, [1, 2, 3, 4, 8]),
        };

        foreach (var (col, rows) in assignments)
        {
            var (min, _) = ColumnRanges[col];
            // Lấy 5 số đầu của column range
            var numbers = Enumerable.Range(min, 5).ToList();
            for (var i = 0; i < 5; i++)
            {
                grid[rows[i]][col] = numbers[i];
            }
        }

        return grid;
    }

    /// <summary>
    /// Serialize grid to JSON format: {"rows": [[3,null,21,...],[...],...]}
    /// </summary>
    public static string SerializeGrid(int?[][] grid)
    {
        var rows = grid.Select(row =>
            "[" + string.Join(",", row.Select(v => v.HasValue ? v.Value.ToString() : "null")) + "]"
        );
        return $"{{\"rows\":[{string.Join(",", rows)}]}}";
    }
}
