using System.Collections.Generic;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace HostedServiceSample.Producer;

[RouterName("test-router")]
[DirectContentType(2)]
public class TestRequestModel { }

public class TestResponseModel
{
	public IEnumerable<TestResponseModelItem> Items { get; set; }
}

public class TestResponseModelItem
{
	public string Value { get; set; }
}