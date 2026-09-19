using Google.Protobuf;
using Pro.SerializationInterop.RVProtoData;
using ScriptureSync.ProPresenter;

namespace ScriptureSync.Tests;

public sealed class ProPresenterNativePlaylistTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "ScriptureSync-native-" + Guid.NewGuid());
    private readonly string _presentation;
    private readonly byte[] _source;
    public ProPresenterNativePlaylistTests()
    {
        Directory.CreateDirectory(Path.Combine(_workspace,"Libraries","Default"));
        _presentation=Path.Combine(_workspace,"Libraries","Default","generated.pro");
        var presentation=ProPresenterTemplateTests.Fixture();
        presentation.Uuid=new UUID { String=Guid.NewGuid().ToString() };
        presentation.Notes="ScriptureSync-owned:test";
        presentation.Name="John 3:16 (TEST)";
        File.WriteAllBytes(_presentation,presentation.ToByteArray());
        var root=new Playlist { Type=Playlist.Types.Type.Root, Playlists=new Playlist.Types.PlaylistArray() };
        var playlist=new Playlist { Uuid=new UUID { String="playlist" }, PcoPlan=new PlanningCenterPlan { PlanIdStr="plan", ParentIdStr="service", PlanTitle="Original title" }, Items=new Playlist.Types.PlaylistItems() };
        playlist.Items.Items.Add(new PlaylistItem { Uuid=new UUID { String="item" }, Name="Sermon", IsHidden=true, PlanningCenter=new PlaylistItem.Types.PlanningCenter { Item=new PlanningCenterPlan.Types.PlanItem { PcoIdStr="pco-item", ParentIdStr="plan", ServiceIdStr="service", Name="Sermon" } } });
        root.Playlists.Playlists.Add(playlist);
        root.Playlists.Playlists.Add(new Playlist { Name="Unrelated", Uuid=new UUID { String="other" } });
        // Preserve unknown native fields as well as known metadata.
        root=Playlist.Parser.ParseFrom(root.ToByteArray().Concat(new byte[]{0xA0,0x06,0x7B}).ToArray());
        using var stream=new MemoryStream();
        using(var output=new CodedOutputStream(stream,true)) {
            output.WriteTag(3,WireFormat.WireType.LengthDelimited); output.WriteBytes(root.ToByteString());
            output.WriteTag(99,WireFormat.WireType.Varint);output.WriteUInt32(123);output.Flush();
        }
        _source=stream.ToArray();
    }
    private byte[] Link(byte[]? bytes=null,string plan="plan",string item="pco-item") => ProPresenterNativePlaylist.PrepareLink(bytes??_source,_workspace,"playlist","item",plan,item,_presentation);
    [Fact]
    public void Adds_nested_link_preserving_pco_identity_flags_unknown_fields_and_other_playlists()
    {
        var result=Link();
        var before=ProPresenterNativePlaylist.ReadRoot(_source);
        var after=ProPresenterNativePlaylist.ReadRoot(result);
        var linked=after.Playlists.Playlists[0].Items.Items[0];
        Assert.Equal("item",linked.Uuid.String);
        Assert.True(linked.IsHidden);
        Assert.Equal(_presentation,linked.PlanningCenter.LinkedData.Presentation.DocumentPath.AbsoluteString);
        Assert.Equal("Libraries/Default/generated.pro",linked.PlanningCenter.LinkedData.Presentation.DocumentPath.Local.Path);
        Assert.Equal(before.Playlists.Playlists[0].PcoPlan,after.Playlists.Playlists[0].PcoPlan);
        Assert.Equal(before.Playlists.Playlists[0].Items.Items[0].PlanningCenter.Item,linked.PlanningCenter.Item);
        linked.Name="Sermon";linked.PlanningCenter.LinkedData=null;
        Assert.Equal(before,after);
        Assert.Equal(_source[^3..],result[^3..]);
        Assert.Null(before.Playlists.Playlists[0].Items.Items[0].PlanningCenter.LinkedData);
    }
    [Fact]
    public void Repeat_is_byte_identical_and_different_existing_link_is_rejected()
    {
        var first=Link();
        Assert.Equal(first,Link(first));
        var root=ProPresenterNativePlaylist.ReadRoot(first);
        root.Playlists.Playlists[0].Items.Items[0].PlanningCenter.LinkedData.Presentation.DocumentPath.AbsoluteString=Path.Combine(_workspace,"Libraries","Default","someone-elses.pro");
        using var stream=new MemoryStream();
        using(var output=new CodedOutputStream(stream,true)){output.WriteTag(3,WireFormat.WireType.LengthDelimited);output.WriteBytes(root.ToByteString());output.Flush();}
        Assert.Throws<InvalidDataException>(()=>Link(stream.ToArray()));
    }
    [Fact]
    public void Wrong_plan_and_item_are_rejected()
    {
        Assert.Throws<InvalidDataException>(()=>Link(plan:"wrong"));
        Assert.Throws<InvalidDataException>(()=>Link(item:"wrong"));
    }
    [Fact]
    public void Non_owned_presentation_is_rejected()
    {
        var doc=Presentation.Parser.ParseFrom(File.ReadAllBytes(_presentation));doc.Notes="Human presentation";
        File.WriteAllBytes(_presentation,doc.ToByteArray());
        Assert.Throws<InvalidDataException>(()=>Link());
    }
    public void Dispose()=>Directory.Delete(_workspace,true);
}
