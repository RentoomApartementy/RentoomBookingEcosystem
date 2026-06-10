using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace RentoomBookingWeb.Services.Localization
{
    public class LocalizedUrlBuilder
    {
        private readonly string _basePath;
        private readonly List<string> _parameters = new();
        private readonly Dictionary<string, string> _queryParams = new();

        public LocalizedUrlBuilder(string basePath)
        {
            _basePath = basePath.TrimEnd('/');
        }

        public LocalizedUrlBuilder WithParam(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _parameters.Add(value);
            }
            return this;
        }

        public LocalizedUrlBuilder WithOptionalParam(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _parameters.Add(value);
            }
            return this;
        }

        public LocalizedUrlBuilder WithQueryParam(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                _queryParams[key] = value;
            }
            return this;
        }

        public string Build()
        {
            var sb = new StringBuilder(_basePath);

            foreach (var param in _parameters)
            {
                sb.Append('/');
                sb.Append(param);
            }

            if (_queryParams.Any())
            {
                sb.Append('?');
                var query = string.Join("&", _queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                sb.Append(query);
            }

            return sb.ToString();
        }

        public override string ToString() => Build();
    }
}
