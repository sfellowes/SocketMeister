﻿namespace SocketMeister.Testing
{

    /// <summary>
    /// ITest Interface 
    /// </summary>
    internal partial interface ITest<T>
    {
        string Description { get; }
        int Id { get; }
        object Lock { get; }
        string Name { get; }


    }
}
