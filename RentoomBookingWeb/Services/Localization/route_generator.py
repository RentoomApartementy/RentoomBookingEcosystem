import os
import xml.etree.ElementTree as ET
import re
import unicodedata

# Configuration
RES_DIR = '../../Resources'
OUTPUT_FILE = 'LocalizedRouteRegistry.cs'
NAMESPACE = 'RentoomBookingWeb.Services.Localization'

# Mapping: Key -> { RES_FILE_PREFIX, RES_KEY, COMPONENT_FILE, PARAMS }
PAGE_MAPPINGS = {
    'Home': {
        'file_prefix': 'HomePage', 
        'res_key': 'Home_PageTitle',
        'file_path': '../../Components/Features/Home/Pages/Home.razor',
        'params': '',
        'is_home': True
    },
    'Apartments': {
        'file_prefix': 'Apartments', 
        'res_key': 'ApartmentsText',
        'file_path': '../../Components/Features/Apartments/Pages/Apartments.razor',
        'params': '',
        'extra_params': '/{StartDate}/{EndDate}/{Adults?}/{Children?}'
    },
    'Contact': {
        'file_prefix': 'Contact', 
        'res_key': 'ContactText',
        'file_path': '../../Components/Features/Contact/Pages/Contact.razor',
        'params': ''
    },
    'Cooperation': {
        'file_prefix': 'Cooperation', 
        'res_key': 'CooperationText',
        'file_path': '../../Components/Features/Cooperation/Pages/Cooperation.razor',
        'params': ''
    },
    'Statute': {
        'file_prefix': 'StatutePage', 
        'res_key': 'StatuteHeader',
        'file_path': '../../Components/Features/Statute/Pages/Statute.razor',
        'params': '/{Id:int}/{Slug?}'
    },
    'AboutCity': {
        'file_prefix': 'SharedResources', 
        'res_key': 'AboutCity',
        'file_path': '../../Components/Features/TorunLocation/Pages/TorunLocation.razor',
        'params': ''
    },
    'ApartmentDetail': {
        'file_prefix': 'Apartments', 
        'res_key': 'ApartmentsText',
        'file_path': '../../Components/Features/ReservationWorkflow/Pages/ApartmentPage.razor',
        'params': '/{id:int}/{slug}/{startDate?}/{endDate?}/{adults?}/{children?}'
    },
    'AllApartments': {
        'file_prefix': 'Apartments', 
        'res_key': 'AllApartments_BreadcrumbName',
        'file_path': '../../Components/Features/AllApartments/Pages/AllApartments.razor',
        'params': ''
    }
}

def to_slug(phrase):
    if not phrase:
        return ""
    
    # Map Polish characters explicitly before normalization
    polish_map = {
        'ą': 'a', 'ć': 'c', 'ę': 'e', 'ł': 'l', 'ń': 'n', 'ó': 'o', 'ś': 's', 'ź': 'z', 'ż': 'z',
        'Ą': 'a', 'Ć': 'c', 'Ę': 'e', 'Ł': 'l', 'Ń': 'n', 'Ó': 'o', 'Ś': 's', 'Ź': 'z', 'Ż': 'z'
    }
    for pol, lat in polish_map.items():
        phrase = phrase.replace(pol, lat)

    nks = unicodedata.normalize('NFKD', phrase)
    res = "".join([c for c in nks if not unicodedata.combining(c)])
    res = res.lower()
    res = re.sub(r'[^a-z0-9]+', '-', res)
    return res.strip('-')

def extract_translations():
    mapping = {}
    files = [f for f in os.listdir(RES_DIR) if f.endswith('.resx')]
    
    all_cultures = set()
    for f in files:
        match = re.match(r'^.*\.([a-z-]+)\.resx$', f, re.IGNORECASE)
        if match:
            all_cultures.add(match.group(1).lower())
    all_cultures.add("pl")

    for key, config in PAGE_MAPPINGS.items():
        mapping[key] = {}
        prefix = config['file_prefix']
        res_key = config['res_key']
        
        for cult in all_cultures:
            mapping[key][cult] = ""

        pattern = re.compile(rf"^{prefix}(\.([a-z-]+))?\.resx$", re.IGNORECASE)
        
        for f in files:
            match = pattern.match(f)
            if match:
                culture = match.group(2) or "pl"
                culture = culture.lower()
                
                if config.get('is_home'):
                    mapping[key][culture] = ""
                    continue

                try:
                    tree = ET.parse(os.path.join(RES_DIR, f))
                    root = tree.getroot()
                    
                    for data in root.findall('data'):
                        if data.get('name') == res_key:
                            # IMPORTANT: Some resx files have multiple <value> tags (one empty).
                            # We need the first non-empty one.
                            for val_node in data.findall('value'):
                                if val_node is not None and val_node.text and val_node.text.strip():
                                    mapping[key][culture] = to_slug(val_node.text)
                                    break
                            break
                except Exception as e:
                    print(f"Error parsing {f}: {e}")
                    
        if key == 'ApartmentDetail':
            for cult in all_cultures:
                if not mapping[key].get(cult) and mapping.get('Apartments', {}).get(cult):
                    mapping[key][cult] = mapping['Apartments'][cult]
                    
    return mapping

def generate_csharp(mapping):
    lines = []
    lines.append("using System;")
    lines.append("using System.Collections.Generic;")
    lines.append("")
    lines.append(f"namespace {NAMESPACE}")
    lines.append("{")
    lines.append("    public static class LocalizedRouteRegistry")
    lines.append("    {")
    lines.append("        public static readonly Dictionary<string, Dictionary<string, string>> PageSlugs = new()")
    lines.append("        {")
    
    for key in sorted(mapping.keys()):
        lines.append(f'            ["{key}"] = new(StringComparer.OrdinalIgnoreCase)')
        lines.append("            {")
        for culture in sorted(mapping[key].keys()):
            slug = mapping[key][culture]
            lines.append(f'                ["{culture}"] = "{slug}",')
        lines.append("            },")
        
    lines.append("        };")
    lines.append("    }")
    lines.append("}")
    return "\n".join(lines)

def apply_fallbacks(mapping):
    print("Applying fallbacks...")
    all_cultures = set()
    for k in mapping:
        all_cultures.update(mapping[k].keys())

    for key, config in PAGE_MAPPINGS.items():
        cultures = mapping.get(key, {})
        is_home = config.get('is_home', False)
        if is_home:
            continue
            
        polish_slug = cultures.get('pl', cultures.get('pl-pl', ''))
        
        for culture in all_cultures:
            slug = cultures.get(culture, "")
            
            # FALLBACK LOGIC
            if not slug:
                slug = polish_slug
            
            # LAST RESORT FALLBACK: If still empty, use the key as slug
            if not slug:
                slug = to_slug(key)
                
            mapping[key][culture] = slug

def patch_razor_files(mapping):
    print("Patching Razor files...")
    start_marker = "@* [SEO_ROUTES] *@"
    end_marker = "@* [SEO_ROUTES_END] *@"
    
    all_cultures = set()
    for k in mapping:
        all_cultures.update(mapping[k].keys())

    for key, config in PAGE_MAPPINGS.items():
        file_path = config['file_path']
        if not os.path.exists(file_path):
            continue
            
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
        if start_marker not in content or end_marker not in content:
            continue
            
        route_lines = set()
        cultures = mapping.get(key, {})
        is_home = config.get('is_home', False)
        params = config.get('params', '')
        extra_params = config.get('extra_params', '')

        for culture in all_cultures:
            short_culture = culture.split('-')[0].lower()
            slug = cultures.get(culture, "")
            
            if not slug or is_home:
                route_lines.add(f'@page "/{short_culture}{params}"')
                if extra_params:
                    route_lines.add(f'@page "/{short_culture}{extra_params}"')
            else:
                route_lines.add(f'@page "/{short_culture}/{slug}{params}"')
                if extra_params:
                    route_lines.add(f'@page "/{short_culture}/{slug}{extra_params}"')
            
        sorted_routes = sorted(list(route_lines))
        replacement_block = start_marker + "\n" + "\n".join(sorted_routes) + "\n" + end_marker
        pattern = re.compile(rf"{re.escape(start_marker)}.*?{re.escape(end_marker)}", re.DOTALL)
        new_content = pattern.sub(replacement_block, content)
        
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Patched {file_path}")

if __name__ == "__main__":
    mapping = extract_translations()
    apply_fallbacks(mapping) # NEW STEP
    csharp_code = generate_csharp(mapping)
    with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
        f.write(csharp_code)
    patch_razor_files(mapping)
    print("Done!")
