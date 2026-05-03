namespace QuestBackend;

public class QuestionLoader
{
    public List<Question> LoadQuestions()
    {
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
            .Build();

        var payload = deserializer.Deserialize<QuestionsYaml>(Questions);

        return payload.Questions.Select(q => new Question
        {
            Text = q.Title,
            Answer1 = q.Options.ElementAtOrDefault(0) ?? string.Empty,
            Answer2 = q.Options.ElementAtOrDefault(1) ?? string.Empty,
            Answer3 = q.Options.ElementAtOrDefault(2) ?? string.Empty,
            Answer4 = q.Options.ElementAtOrDefault(3) ?? string.Empty,
            CorrectAnswer = q.CorrectAnswer,
            Explanation = q.Explanation,
        }).ToList();
    }

    private string Questions = """
    questions:
      - type: interface
        title: "Una interfície"
        options:
          - "És un contracte que defineix mètodes i propietats a implementar"
          - "Es pot instanciar amb new()"
          - "No pot implementar altres interfícies"
          - ".NET no té interfícies"
        correct_answer: 1
        explanation: |
          En C#, una interfície defineix un contracte.

          ```c#
          public interface IUser
          {
              void SetPassword(string password);
              int Edat { get; set; }
          }
          ```

          - No es pot instanciar directament.
          - Sí que pot heretar d'altres interfícies.

      - type: abstract_class
        title: "Una classe abstracta NO pot"
        options:
          - "Instanciar-se amb new()"
          - "Heretar d'altres classes"
          - "Implementar interfícies"
          - "Tenir mètodes abstractes i implementats"
        correct_answer: 1
        explanation: |
          Una classe abstracta no es pot instanciar directament.

          ```c#
          public abstract class UserBase : IUser
          {
              public abstract void SetPassword(string password);
              public int Edat { get; set; }
          }
          ```

          - Pot heretar d'una altra classe.
          - Pot implementar interfícies.
          - Pot tenir mètodes abstractes i implementats.
    """;

    private class QuestionsYaml
    {
        public required List<QuestionYaml> Questions { get; set; }
    }

    private class QuestionYaml
    {
        public string? Type { get; set; }
        public required string Title { get; set; }
        public required List<string> Options { get; set; }
        public required int CorrectAnswer { get; set; }
        public required string Explanation { get; set; }
    }

}
