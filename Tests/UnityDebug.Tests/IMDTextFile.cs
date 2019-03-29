using System;

namespace Mono.Debugging.Tests {
    public interface IMDTextFile
    {
        string Name { get; }

        string Text { get; }
        int Length { get; }
		
        string GetText (int startPosition, int endPosition);
        char GetCharAt (int position);
		
        int GetPositionFromLineColumn (int line, int column);
        void GetLineColumnFromPosition (int position, out int line, out int column);
    }
}