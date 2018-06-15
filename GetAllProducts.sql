select Products.Product_name, Categorys.Category_name from  Products
left join Products_Categorys on Products.products_id=Products_Categorys.products_id
left join Categorys on Categorys.category_id=Products_Categorys.category_id
order by Products.products_id
