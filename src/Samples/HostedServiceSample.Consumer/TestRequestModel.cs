using System.Collections.Generic;

namespace HostedServiceSample.Consumer;

public class TestRequestModel { }

public class TestResponseModel
{
	public IEnumerable<TestResponseModelItem> Items { get; set; }
}

public class TestResponseModelItem
{
	public string Value { get; set; }
}