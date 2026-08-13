public sealed class DungeonRuntimeContext
{
    public DungeonRunSession Session { get; }
    public DungeonPage Page { get; }
    public DungeonFieldView FieldView { get; }
    public DungeonBoardView Board => FieldView != null
        ? FieldView.Board
        : null;
    public BattleManager BattleManager { get; }

    internal DungeonRuntimeContext(
        DungeonRunSession session,
        DungeonPage page,
        DungeonFieldView fieldView,
        BattleManager battleManager)
    {
        Session = session;
        Page = page;
        FieldView = fieldView;
        BattleManager = battleManager;
    }
}
