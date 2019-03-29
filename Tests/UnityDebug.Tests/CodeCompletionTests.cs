//
// CodeCompletionTests.cs
//
// Author:
//       David Karlaš <david.karlas@microsoft.com>
//
// Copyright (c) 2017 Microsoft Corp.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using NUnit.Framework;

namespace VSCode.UnityDebug.Tests
{
	namespace Soft
	{
		[TestFixture]
		public class SdbCodeCompletionTests : CodeCompletionTests
		{
			public SdbCodeCompletionTests () : base ("Mono.Debugger.Soft")
			{
			}
		}
	}

	namespace Win32
	{
		[TestFixture]
		[Platform (Include = "Win")]
		public class CorCodeCompletionTests : CodeCompletionTests
		{
			public CorCodeCompletionTests () : base ("MonoDevelop.Debugger.Win32")
			{
			}
		}
	}

	[TestFixture]
	public abstract class CodeCompletionTests : DebugTests
	{
		public CodeCompletionTests (string engineId) : base (engineId)
		{
		}

		[OneTimeSetUp]
		public override void SetUp ()
		{
			base.SetUp ();

			Start ("TestEvaluation");
		}

		[Test]
		public void SimpleVariablesList ()
		{
			var completionData = Session.ActiveThread.Backtrace.GetFrame (0).GetExpressionCompletionData ("");
			Assert.Less (0, completionData.Items.Count);
		}
	}
}
