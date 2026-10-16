using System.Text.Json.Serialization;

namespace SharedKernel.UnitTests;

// K5: one contract for every list. The shared type reads and checks page, pageSize, sort and q; a filter reads the JSON names of its
// enum. JSON:API 1.1, "Sorting": one field, descending with a leading minus.
public class ListRequestTests
{
    [Fact]
    public void CreateList_NothingSent_SortsByTheFirstFieldAscendingWithoutSearch()
    {
        var list = ListRequest.Create<ThingSort>(null, null, null, null);

        list.Value.ShouldBe(new ListRequest<ThingSort>(new PageRequest(1, 50), ThingSort.Name, Descending: false, Search: null));
    }

    [Theory]
    [InlineData("name", ThingSort.Name, false)]
    [InlineData("lastName", ThingSort.LastName, false)]
    [InlineData("-name", ThingSort.Name, true)]
    [InlineData("-lastName", ThingSort.LastName, true)]
    public void CreateList_ASortField_SortsByItInTheDirectionOfItsSign(string sort, ThingSort field, bool descending)
    {
        var list = ListRequest.Create<ThingSort>(null, null, Sort(sort), null);

        (list.Value.Sort, list.Value.Descending).ShouldBe((field, descending));
    }

    // A value the query string sends empty is a sort left out.
    [Fact]
    public void CreateList_AnEmptySort_SortsByTheFirstFieldAscending()
    {
        var list = ListRequest.Create<ThingSort>(null, null, Sort(string.Empty), null);

        (list.Value.Sort, list.Value.Descending).ShouldBe((ThingSort.Name, false));
    }

    [Theory]
    [InlineData("age")]
    [InlineData("Name")]
    [InlineData("LastName")]
    [InlineData("-")]
    [InlineData("--name")]
    [InlineData("+name")]
    [InlineData("name,lastName")]
    [InlineData("0")]
    [InlineData(" name")]
    public void CreateList_AnUnknownSort_IsRejected(string sort)
    {
        var list = ListRequest.Create<ThingSort>(null, null, Sort(sort), null);

        list.Error.Code.ShouldBe(ErrorCode.PagingSortInvalid);
    }

    [Fact]
    public void CreateList_ASearch_IsTrimmed()
    {
        var list = ListRequest.Create<ThingSort>(null, null, null, "  acme  ");

        list.Value.Search.ShouldBe("acme");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateList_AnEmptySearch_IsNoSearch(string q)
    {
        var list = ListRequest.Create<ThingSort>(null, null, null, q);

        list.Value.Search.ShouldBeNull();
    }

    [Fact]
    public void CreateList_ASearchOf100CharactersWithSpacesAround_IsAccepted()
    {
        var list = ListRequest.Create<ThingSort>(null, null, null, $" {new string('a', 100)} ");

        list.Value.Search.ShouldBe(new string('a', 100));
    }

    [Fact]
    public void CreateList_ASearchOf101Characters_IsRejected()
    {
        var list = ListRequest.Create<ThingSort>(null, null, null, new string('a', 101));

        list.Error.Code.ShouldBe(ErrorCode.PagingQInvalid);
    }

    [Theory]
    [InlineData(0, null, ErrorCode.PagingPageInvalid)]
    [InlineData(null, 0, ErrorCode.PagingPageSizeInvalid)]
    [InlineData(null, 101, ErrorCode.PagingPageSizeInvalid)]
    public void CreateList_PagingOutsideItsRange_IsRejectedWithThePagingCode(int? page, int? pageSize, ErrorCode code)
    {
        var list = ListRequest.Create<ThingSort>(page, pageSize, null, null);

        list.Error.Code.ShouldBe(code);
    }

    [Fact]
    public void DescribeSort_OfAList_IsEachFieldInBothDirections()
    {
        ListSort.ValuesOf<ThingSort>().ShouldBe(["name", "-name", "lastName", "-lastName"]);
    }

    // Binding never fails, so an unknown value reaches the check and answers with its code.
    [Fact]
    public void BindSort_AnyText_IsKeptForTheCheck()
    {
        ListSort<ThingSort>.TryParse("age", out var sort).ShouldBeTrue();

        sort.Text.ShouldBe("age");
    }

    [Theory]
    [InlineData("on-hold", ThingStatus.OnHold)]
    [InlineData("active", ThingStatus.Active)]
    public void ReadFilter_TheJsonNameOfAValue_GivesTheValue(string text, ThingStatus status)
    {
        var filter = ListFilter.Read<ThingStatus>(new ListFilter<ThingStatus>(text), "status");

        filter.Value.ShouldBe(status);
    }

    [Fact]
    public void ReadFilter_LeftOut_GivesNoValue()
    {
        var filter = ListFilter.Read<ThingStatus>(null, "status");

        filter.Value.ShouldBeNull();
    }

    // A value the query string sends empty is a filter left out.
    [Fact]
    public void ReadFilter_AnEmptyValue_GivesNoValue()
    {
        var filter = ListFilter.Read<ThingStatus>(new ListFilter<ThingStatus>(string.Empty), "status");

        filter.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("OnHold")]
    [InlineData("Active")]
    [InlineData("1")]
    [InlineData("deleted")]
    [InlineData(" ")]
    public void ReadFilter_AnUnknownValue_IsRejected(string text)
    {
        var filter = ListFilter.Read<ThingStatus>(new ListFilter<ThingStatus>(text), "status");

        filter.Error.Code.ShouldBe(ErrorCode.PagingFilterInvalid);
    }

    [Fact]
    public void DescribeFilter_OfAnEnum_IsTheJsonNameOfEachValue()
    {
        ListFilter.ValuesOf<ThingStatus>().ShouldBe(["on-hold", "active"]);
    }

    [Fact]
    public void BindFilter_AnyText_IsKeptForTheCheck()
    {
        ListFilter<ThingStatus>.TryParse("deleted", out var filter).ShouldBeTrue();

        filter.Text.ShouldBe("deleted");
    }

    private static ListSort<ThingSort> Sort(string text) => new(text);
}

public enum ThingSort
{
    Name,
    LastName,
}

[JsonConverter(typeof(JsonStringEnumConverter<ThingStatus>))]
public enum ThingStatus
{
    [JsonStringEnumMemberName("on-hold")]
    OnHold,

    Active,
}
