namespace SharedKernel.UnitTests;

// K5's exception: the audit logs page by a cursor, newest first. The cursor holds the last item's moment and id; a cursor that does not
// decode or was changed is refused, and so is a limit out of range.
public class CursorRequestTests
{
    private static readonly CursorPosition Position = new(new DateTimeOffset(2026, 10, 16, 9, 30, 15, 123, 456, TimeSpan.Zero), Guid.CreateVersion7());

    [Fact]
    public void CreateCursorRequest_NothingSent_IsTheFirstPageOfFifty()
    {
        var request = CursorRequest.Create(null, null);

        request.Value.ShouldBe(new CursorRequest(50, null));
    }

    // A value the query string sends empty is a cursor left out.
    [Fact]
    public void CreateCursorRequest_AnEmptyCursor_IsTheFirstPage()
    {
        var request = CursorRequest.Create(string.Empty, 10);

        request.Value.ShouldBe(new CursorRequest(10, null));
    }

    [Fact]
    public void CreateCursorRequest_ACursorAListGave_StartsAfterItsPosition()
    {
        var request = CursorRequest.Create(Cursor.Encode(Position), 1);

        request.Value.ShouldBe(new CursorRequest(1, Position));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public void CreateCursorRequest_ALimitOutOfRange_IsRefused(int limit)
    {
        var request = CursorRequest.Create(null, limit);

        request.Error.ShouldBe(Error.Validation(ErrorCode.PagingLimitInvalid, "The limit is 1 to 100."));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("!!!!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void CreateCursorRequest_ACursorThatDoesNotDecode_IsRefused(string cursor)
    {
        var request = CursorRequest.Create(cursor, null);

        request.Error.Code.ShouldBe(ErrorCode.PagingCursorInvalid);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(12)]
    [InlineData(30)]
    public void CreateCursorRequest_ACursorWithOneCharacterChanged_IsRefused(int place)
    {
        var cursor = Changed(Cursor.Encode(Position), place);

        var request = CursorRequest.Create(cursor, null);

        request.Error.Code.ShouldBe(ErrorCode.PagingCursorInvalid);
    }

    [Fact]
    public void BuildCursorPage_MoreFoundThanTheLimit_KeepsTheLimitAndPointsAfterTheLastItem()
    {
        var found = new[] { Position, Position with { Id = Guid.CreateVersion7() }, Position with { Id = Guid.CreateVersion7() } };

        var page = CursorPage.Of(found, new CursorRequest(2, null), item => item);

        page.Items.ShouldBe(found[..2]);
        Cursor.Decode(page.NextCursor!).ShouldBe(found[1]);
    }

    [Fact]
    public void BuildCursorPage_NoMoreThanTheLimitFound_IsTheLastPage()
    {
        var found = new[] { Position };

        var page = CursorPage.Of(found, new CursorRequest(1, null), item => item);

        page.Items.ShouldBe(found);
        page.NextCursor.ShouldBeNull();
    }

    // The cursor with the character at the place replaced by another one.
    private static string Changed(string cursor, int place) => string.Concat(cursor[..place], cursor[place] == 'A' ? "B" : "A", cursor[(place + 1)..]);
}
