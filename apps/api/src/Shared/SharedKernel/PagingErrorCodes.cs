namespace SharedKernel;

/// <summary>The codes of the list and cursor contracts (K5).</summary>
public static class PagingErrorCodes
{
    public static readonly ErrorCode CursorInvalid = ErrorCode.Declare("paging.cursor_invalid");

    public static readonly ErrorCode FilterInvalid = ErrorCode.Declare("paging.filter_invalid");

    public static readonly ErrorCode LimitInvalid = ErrorCode.Declare("paging.limit_invalid");

    public static readonly ErrorCode PageInvalid = ErrorCode.Declare("paging.page_invalid");

    public static readonly ErrorCode PageSizeInvalid = ErrorCode.Declare("paging.page_size_invalid");

    public static readonly ErrorCode QInvalid = ErrorCode.Declare("paging.q_invalid");

    public static readonly ErrorCode SortInvalid = ErrorCode.Declare("paging.sort_invalid");
}
