namespace QuestBackend;

public class Question
{
    public required string Text { get; set; }
    public required string Answer1 { get; set; }
    public required string Answer2 { get; set; }
    public required string Answer3 { get; set; }
    public required string Answer4 { get; set; }
    public required int CorrectAnswer { get; set; }
    public required string Explanation { get; set; }

}