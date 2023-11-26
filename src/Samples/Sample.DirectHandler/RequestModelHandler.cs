using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Protocol;

namespace Sample.Consumer;

public class RequestModelHandler : IHorseRequestHandler<RequestModel, ResponseModel>
{
    public Task<ResponseModel> Handle(RequestModel request, HorseMessage rawMessage, HorseClient client)
    {
        return Task.FromResult(new ResponseModel { Foo = "Foo", No = 1923 });
    }

    public Task<ErrorResponse> OnError(Exception exception, RequestModel request, HorseMessage rawMessage, HorseClient client)
    {
        return Task.FromResult(new ErrorResponse());
    }
}