using System;

namespace Vulperonex.Application.Members;

public sealed class MemberConcurrencyException : Exception
{
    public MemberConcurrencyException(string message) : base(message)
    {
    }
}
