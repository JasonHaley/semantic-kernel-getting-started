using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Neo4j.Console.PropertyGraph;
internal class Utilities
{
    public static string CreateId(string text)
    {
        using (SHA1 sha1 = SHA1.Create()) // TODO: should I use a different hash?
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(text);
            byte[] hashBytes = sha1.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2").ToLower());
            }
            return sb.ToString();
        }
    }
    public static EntityMetadata PopulateEntityMetadata(ChunkMetadata chunkMetadata, TripletRow triplet, EntityMetadata entityMetadata, bool isHead = true)
    {
        if (isHead)
        {
            entityMetadata.name = CreateName(triplet.head);
            entityMetadata.type = triplet.head_type;
            entityMetadata.text = triplet.head;
        }
        else
        {
            entityMetadata.name = CreateName(triplet.tail);
            entityMetadata.type = triplet.tail_type;
            entityMetadata.text = triplet.tail;
        }

        entityMetadata.id = CreateId(entityMetadata.name);
        entityMetadata.mentionedInChunks.Add(chunkMetadata.id, chunkMetadata);

        return entityMetadata;
    }

    public static string CreateName(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Split the text into words
        string[] words = text.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

        StringBuilder nameText = new StringBuilder();

        foreach (string word in words)
        {
            // Capitalize the first letter and make the rest lowercase
            var lword = word;
            if (char.IsDigit(word[0]))
            {
                lword = "_" + word;
            }

            nameText.Append(lword.ToLower());
        }
        return Regex.Replace(nameText.ToString(), "[^a-zA-Z0-9_]", "");
    }
}
