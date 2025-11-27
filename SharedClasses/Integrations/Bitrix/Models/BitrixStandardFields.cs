using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.Bitrix.Models
{
    public class BitrixStandardField
    {
        public string Type { get; set; }
        // public bool IsRequired { get; set; }
        //  public bool IsReadOnly { get; set; }
        // public bool IsImmutable { get; set; }
        //  public bool IsMultiple { get; set; }
        //  public bool IsDynamic { get; set; }
        public string Title { get; set; }
    }

    public class BitrixCustomField : BitrixStandardField
    {
        public string ListLabel { get; set; }
        public string FormLabel { get; set; }
        public string FilterLabel { get; set; }
        // public dynamic Settings { get; set; }
        public List<BitrixFieldItem> Items { get; set; } // For enumeration type fields
                                                         //  public string Popup { get; set; } // For URL type fields
                                                         //  public List<string> Extensions { get; set; } // For file type fields
        public BitrixDateTimeSettings DefaultValue { get; set; } // For datetime type fields
        public bool UseSecond { get; set; } // For datetime type fields
        public bool UseTimezone { get; set; } // For datetime type fields
    }

    public class BitrixFieldItem
    {
        public string ID { get; set; }
        public string Value { get; set; }
    }

    public class BitrixDateTimeSettings
    {
        public string Type { get; set; }
        public string Value { get; set; }
    }

    public class BitrixEmailPhoneField
    {

        public string Value_type { get; set; }
        public string Value { get; set; }
        public string Type_Id { get; set; }

    }

    public class BitrixFieldsDefinition
    {
        public Dictionary<string, BitrixStandardField> Fields { get; set; } = new Dictionary<string, BitrixStandardField>();
        public Dictionary<string, BitrixCustomField> CustomFields { get; set; } = new Dictionary<string, BitrixCustomField>();

        public BitrixFieldsDefinition(string ApiResponsString)
        {
            DeserializeBitrixFieldsDefinition(ApiResponsString);
        }

        private void DeserializeBitrixFieldsDefinition(string jsonStringResponse)
        {
            var jsonObject = JObject.Parse(jsonStringResponse);

            foreach (var property in jsonObject["result"].Children<JProperty>())
            {
                if (property.Name.StartsWith("UF_CRM_"))
                {
                    var pVal = property.Value;
                    var customField = property.Value.ToObject<BitrixCustomField>();

                    // Additional parsing based on the type of the custom field
                    switch (customField.Type)
                    {
                        case "url":
                            // customField.Popup = property.Value["settings"]["POPUP"].ToString();
                            break;
                        case "file":
                            //  customField.Extensions = property.Value["settings"]["EXTENSIONS"].ToObject<List<string>>();
                            break;
                        case "datetime":
                            customField.DefaultValue = property.Value["settings"]["DEFAULT_VALUE"].ToObject<BitrixDateTimeSettings>();
                            customField.UseSecond = property.Value["settings"]["USE_SECOND"].ToString() == "Y";
                            customField.UseTimezone = property.Value["settings"]["USE_TIMEZONE"].ToString() == "Y";
                            break;
                    }

                    CustomFields.Add(property.Name, customField);
                }
                else
                {
                    var standardField = property.Value.ToObject<BitrixStandardField>();
                    Fields.Add(property.Name, standardField);
                }
            }

        }
    }

    public class BitrixDealField
    {
        public object? Value { get; set; }
        public string? ValueType { get; set; }
        public string? Title { get; set; }
        public string? Label { get; set; }
        public string FieldID { get; set; } = string.Empty;
    }

    public class BitrixDealForm
    {
        public BitrixResponseObject DealForm { get; set; }
        public BitrixResponseObject CustomerInfo { get; set; }
    }

    public class BitrixResponseObject
    {
        public List<BitrixDealField> DealData { get; set; } = new();

        public BitrixResponseObject()
        {
            DealData = new();
        }

        public BitrixResponseObject(string jsonReponse, BitrixFieldsDefinition fieldsDef)
        {
            DeserializeBitrixDeal(jsonReponse, fieldsDef);
        }

        private void DeserializeBitrixDeal(string jsonReponse, BitrixFieldsDefinition fieldsDef)
        {
            var jsonObject = JObject.Parse(jsonReponse);

            foreach (var property in jsonObject["result"].Children<JProperty>())
            {
                var field = new BitrixDealField();
                object fValueTemp;
                List<BitrixEmailPhoneField> arr = new();


                field.FieldID = property.Name;
                

                if (property.Value.Type == JTokenType.Array && (property.Name.ToLower().StartsWith("email") || property.Name.ToLower().StartsWith("phone")))
                {
                    arr = property.Value.ToObject<List<BitrixEmailPhoneField>>();

                    field.Value = property.Value.Type == JTokenType.Array ? arr : property.Value.ToString();
                }

                if  (property.Value.Type == JTokenType.String)
                {
                    field.Value = property.Value.ToString();
                }

                if (property.Name.StartsWith("UF_CRM_"))
                    {

                        if (fieldsDef.CustomFields.TryGetValue(property.Name, out var fieldDef))
                        {

                            field.Label = !string.IsNullOrEmpty(fieldDef.ListLabel) ? fieldDef.ListLabel :
                                           !string.IsNullOrEmpty(fieldDef.FormLabel) ? fieldDef.FormLabel :
                                           !string.IsNullOrEmpty(fieldDef.FilterLabel) ? fieldDef.FilterLabel :
                                           property.Name;
                            field.ValueType = fieldDef.Type;
                            field.Title = fieldDef.Title;


                        }

                        if (field.ValueType == "enumeration" && !String.IsNullOrEmpty(field?.Value?.ToString()))
                        {
                            //match id in value with a display name of the value for enumeration fields.
                            var fieldDefEnum = fieldsDef.CustomFields.FirstOrDefault(f => f.Key == property.Name).Value;
                            var selectedEnum = fieldDefEnum.Items.FirstOrDefault(i => i.ID.ToString() == field.Value.ToString());
                            field.Value = selectedEnum?.Value;
                        }


                    }
                    else
                    {
                        if (fieldsDef.Fields.TryGetValue(property.Name, out var fieldDef))
                        {
                            field.Label = fieldDef.Title;
                            field.ValueType = fieldDef.Type;
                            field.Title = fieldDef.Title;

                        }
                    }
                    //adjist field value - find enumeration name based on id.


                    DealData.Add(field);
                

            }
        }
    }
}
