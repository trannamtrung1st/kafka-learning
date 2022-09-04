import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { dynamicUrls } from '../constants/urls.const';

import { parseUrl } from '../helpers/url.helper';

import { ProductModel } from '../models/product.model';
import { SimpleFilterModel } from '../models/simple-filter.model';

@Injectable({
  providedIn: 'root'
})
export class ProductService {

  constructor(private _httpClient: HttpClient) { }

  getProducts(filter: SimpleFilterModel) {
    const url = parseUrl('/api/products/filter', dynamicUrls.saleApiUrl);
    return this._httpClient.post<ProductModel[]>(url.toString(), filter);
  }
}