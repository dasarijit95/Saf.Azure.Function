# Saf.Azure.Function


### What does the Azure function do?

This project demonstrates the use of Azure functions in the context of Support Actions, where a sample Azure function `GetQuotes` is set up to perform the following

1. Verify the caller by checking the bearer token in the header
2. Get API key for from key-vault using Azure Managed Service Identity (MSI)
3. Use that API key to call third-party API, to get data about the stock symbol passed in the POST body
4. Process that data to suit our output format
5. Call another API to get earnings data for the symbol
6. Return all the information as JSON

The function should be called using a POST request with a valid bearer token and a body that looks like

```json
{
    "ticker" : "msft"
}
```



### How does it help Support Actions?

For Support Actions, this means we can create webhooks to make HTTP calls to this function, pass in parameters and the **same** bearer token used by Support Actions Framework. And that would achieve calling multiple APIs, manipulating returned data, while using the same key-vault secret/certificates. Thus solving the problems with custom DLLs and webhook chaining.