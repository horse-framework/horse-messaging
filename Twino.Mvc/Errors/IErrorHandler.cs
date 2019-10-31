using System;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Mvc.Errors
{
    /// <summary>
    /// Each Twino MVC Request is executed in try/catch block and Twino catches the exception if thrown.
    /// If you want to catch these exceptions, you can implement this interface and create new class,
    /// and set TwinoMvc.ErrorHandler property.
    /// </summary>
    public interface IErrorHandler
    {
        /// <summary>
        /// [async] When an uncatched error has occured in HTTP Request, this method will be called.
        /// </summary>
        Task Error(HttpRequest request, Exception ex);
    }
}
