# Sc.Commons.SitePlaceholders
 Be able to create site specific placeholdersettings by prefixing the keys with the site name.

## Example
We have 2 placeholder settings with these keys:
- `corporate-body`
- `body`

We have a Corporate and Shop site and they both have this part in the main layout:

    @Html.Sitecore().Placeholder("body")

- The Corporate site will use the settings in defined `corporate-body` 
- The Shop site will use the settings defined in `body`.
